using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTestLibrary
{
    /// <summary>
    /// Создаёт поток чтения/преобразования блока данных
    /// </summary>
    public class BlockThread : ABlockThread
    {
        #region private
        /// <summary>
        /// Флаг указывающий на то, что для объекта уже была выполнена операция Dispose
        /// </summary>
        private bool _Disposed = false;

        /// <summary>
        /// Поток выполняющий операцию обработки данных
        /// </summary>
        private Thread _Thread;
        #endregion

        /// <summary>
        /// Передача параметров блока потоковой обработки данных
        /// (не знаю как это сделать через конструктор, с учётом абстрактного класса и типизации фабрики для возможности замены этого компонента системы на другой)
        /// Создаёт поток обработки данных
        /// </summary>
        /// <param name="factory">Управляющая фабрика</param>
        /// <param name="Id">Идентификатор блока</param>
        /// <param name="position">Позиция блока в исходном файле</param>
        public override void SetSettings(AGZipThreadFactory factory, long id, long position)
        {
            base.SetSettings(factory, id, position);

            _Thread = new Thread(this.ThreadAction)
            {
                Name = $"Чтение [{this.Id}]"
            };
        }

        /// <summary>
        /// Запускает поток обработки данных
        /// </summary>
        public override void Start()
        {
            _Thread.Start();
        }

        #region деструктор
        /// <summary>
        /// Деструктор
        /// </summary>
        ~BlockThread()
        {
            Dispose(false);
        }

        /// <summary>
        /// Реализация механизма для освобождения неуправляемых ресурсов
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Реализация механизма для освобождения неуправляемых ресурсов
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this._Disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                }

                // Call the appropriate methods to clean up
                // unmanaged resources here.
                // If disposing is false,
                // only the following code is executed.
                base.Dispose();
                _Thread = null;

                // Note disposing has been done.
                _Disposed = true;
            }
        }
        #endregion

        /// <summary>
        /// Метод обрабоки данных в потоке
        /// </summary>
        private void ThreadAction()
        {
            if (!_Factory.ErrorStatus)
            {
                Console.WriteLine($"{_Thread.Name}...");

                if (_Factory.Mode == CompressionMode.Compress)
                    Compress();
                else Decompress();
            }

            if (!_Factory.ErrorStatus)
            {
                Console.WriteLine($"{_Thread.Name} - ОК");

                //Передаём обработку события завершения операции в основной поток приложения
                Compleated?.BeginInvoke(this, EventArgs.Empty, null, null);
            }
        }

        /// <summary>
        /// Считывает блок данных исходного файла и архивирует их
        /// </summary>
        private void Compress()
        {
            try
            {
                using (var sourceStream = _Factory.SourceFile.OpenRead())
                using (var targetStream = new MemoryStream())
                {
                    using (var zipStream = new GZipStream(targetStream, CompressionMode.Compress))
                    {
                        //задаём длинну блока
                        var restOf = _Factory.SourceFile.Length - _Position;
                        int _BlockSize = restOf > _Factory.MaxBlockSize ? _Factory.MaxBlockSize : (int)restOf;
                        Data = new byte[_BlockSize];

                        //считываем данные
                        sourceStream.Position = _Position;
                        sourceStream.Read(Data, 0, _BlockSize);

                        //преобразуем данные
                        zipStream.Write(Data, 0, Data.Length);
                    }
                    //сохраняем результат
                    Data = targetStream.ToArray();
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine($"Ошибка сжатия данных: блок \"{Id}\" ({exp.Message})");
                ErrorStatus = true;
            }
        }

        /// <summary>
        /// Считывает блок данных исходного файла и разархивирует их
        /// </summary>
        private void Decompress()
        {
            try
            {
                using (var sourceStream = _Factory.SourceFile.OpenRead())
                {
                    sourceStream.Position = _Position;

                    //получаем длинну блока сжатых данных
                    byte[] lengthBuffer = new byte[8];
                    sourceStream.Read(lengthBuffer, 0, lengthBuffer.Length);
                    int blockLength = BitConverter.ToInt32(lengthBuffer, 4);
                    byte[] zipBuffer = new byte[blockLength];
                    lengthBuffer.CopyTo(zipBuffer, 0);

                    //считываем блок данных
                    sourceStream.Read(zipBuffer, 8, blockLength - 8);
                    int _dataSize = BitConverter.ToInt32(zipBuffer, blockLength - 4);
                    Data = new byte[_dataSize];

                    using (var ms = new MemoryStream(zipBuffer))
                    using (var zipStream = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        //разархивируем и сохраняем результат
                        zipStream.Read(Data, 0, Data.Length);
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine($"Ошибка разархивации данных: блок \"{Id}\" ({exp.Message})");
                ErrorStatus = true;
            }
        }
    }
}
