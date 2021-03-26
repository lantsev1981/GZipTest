using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTestLibrary
{

    /// <summary>
    /// Фабрика создания потоков для поблочного считывания и архивации/разархивации файлов
    /// </summary>
    public class GZipThreadFactory<T> : AGZipThreadFactory where T : ABlockThread, new()
    {
        #region private
        /// <summary>
        /// Общее количество блоков исходного файла
        /// Для операции архивации блоки составляются изсходя из размера MaxPartSize
        /// </summary>
        private long _BlockCount;

        /// <summary>
        /// Индекс следующего блока в очереди на чтения/преобразования
        /// </summary>
        private long _BlockToRead;

        /// <summary>
        /// Индекс следующего блока в очереди на запись в выходной файл
        /// </summary>
        private long _BlockToWrite;

        /// <summary>
        /// Объект блокировки, необходим для соблюдения очереди создания новых потоков на чтение/преобразование
        /// </summary>
        private readonly object _NewBlockThreadLoker = new object();

        /// <summary>
        /// Словарь, содержит позицию блока в исходном сжатом файле
        /// Формируется только при опреации разархивации данных, на этапе предварительной обработке файла
        /// Необходим, так как операцию разархивации невозможно произвести без информации о количестве блоков и их размере
        /// </summary>
        private Dictionary<long, long> _ZipBlokPosDict = new Dictionary<long, long>();

        /// <summary>
        /// Словарь, содержит информацию о завершённых операциях поблочного считывания/преобразования данных
        /// Необходим для формирования очереди записи данных в выходной файл
        /// Операция записи производится основным потоком приложения, так как одновременная запись несколькими потоками не допускается
        /// </summary>
        private Dictionary<long, ABlockThread> _CompleatedBTDict = new Dictionary<long, ABlockThread>();

        /// <summary>
        /// Базовая информация об исходном файле
        /// </summary>
        public FileInfo _TargetFile { get; protected set; }

        /// <summary>
        /// Выходной поток записи данных
        /// Создаётся в конструкторе и закрывается при завершении всех операций записи
        /// </summary>
        private FileStream _TargetFileStream;

        /// <summary>
        /// Счётчик времени выполнения всех операций
        /// </summary>
        private Stopwatch _Stopwatch = new Stopwatch();

        /// <summary>
        /// Блокирут выход в основной поток до завершения всех опираций
        /// </summary>
        private static AutoResetEvent waitHandler = new AutoResetEvent(false);
        #endregion

        /// <summary>
        /// Осуществляет многопоточную архивировацию/разархивировацию данных
        /// </summary>
        /// <param name="mode">Режим работы архивация/разархивация</param>
        /// <param name="sourceFile">Базовая информация об исходном файле</param>
        /// <param name="targetFile">Базовая информация о выходном файле</param>
        public GZipThreadFactory(CompressionMode mode, FileInfo sourceFile, FileInfo targetFile)
        {
            Mode = mode;
            SourceFile = sourceFile;
            _TargetFile = targetFile;
        }

        /// <summary>
        /// Запускает процесс обработки данных
        /// </summary>
        public void Start()
        {
            _Stopwatch.Start();
            _TargetFileStream = _TargetFile.Create();
            //Определение общего количества операций чтения/преобразования в зависимости от максимально размера блока
            if (Mode == CompressionMode.Compress)
                _BlockCount = SourceFile.Length / MaxBlockSize + 1;
            else
                ReadZipBlock();

            if (!ErrorStatus)
            {
                //Запуск первых операций поблочного чтения/преобразования данных.
                //Максимальное количество паралельных потоков определяется исходя из количества логических ядер процессора
                for (int i = 0; i < Math.Min(Environment.ProcessorCount - 1, _BlockCount); i++)
                    StartNewBlockThread();
            }

            waitHandler.WaitOne();
        }

        /// <summary>
        /// Создаёт новый поток чтения/преобразования блока данных
        /// Подписывается на событие завершения операции и запускает её
        /// </summary>
        private void StartNewBlockThread()
        {
            var position = _ZipBlokPosDict.TryGetValue(_BlockToRead, out long zbp) ? zbp : _BlockToRead * MaxBlockSize;
            var bt = new T();
            bt.SetSettings(this, _BlockToRead++, position);
            bt.Compleated += BlockThread_Compleated;
            bt.Start();
        }

        /// <summary>
        /// Определяет позицию каждого блока в исходном сжатом файле и сохраняет данные в словаре _ZipBlokPosDict
        /// </summary>
        private void ReadZipBlock()
        {
            try
            {
                Console.WriteLine($"Вычисление количества блоков сжатого файла...");
                using (var sourceStream = SourceFile.OpenRead())
                {
                    while (sourceStream.Position < sourceStream.Length)
                    {
                        _ZipBlokPosDict.Add(_BlockCount, sourceStream.Position);
                        byte[] buffer = new byte[8];
                        sourceStream.Read(buffer, 0, buffer.Length);
                        int blockLength = BitConverter.ToInt32(buffer, 4);
                        sourceStream.Position += blockLength - 8;
                        _BlockCount++;
                    }
                }
                Console.WriteLine($"Вычисление количества блоков сжатого файла - ОК");
            }
            catch (Exception exp)
            {
                Console.WriteLine($"Ошибка предобработки файла: ({exp.Message})");
                Console.WriteLine($"Возможно файл имеет неверный формат");
                ErrorStatus = true;
                Console.WriteLine("Для выхода из программы нажмите Enter");
                Environment.ExitCode = 1;
            }
        }

        /// <summary>
        /// Реализует основную логику работы программы
        /// Обработчик события завершения операции чтения/преобразования блока данных
        /// Запускает обработку следующего блока данных
        /// Чтение/преобразование данных работает в многопоточном режиме
        /// Очередь готовых блоков формируется по мере завершения обработки блока
        /// Один из потоков инициирует запись в выходной файл основным потоком
        /// Пока идёт запись данных, продолжается подготовка новых блоков
        /// Запись выходного файла продолжается пока соблюдается очередность готовых блоков
        /// Завершение записи выходного файла разблокирует доступ к списку готовых блоков и процесс повторяется пока не будут обработаны и записаны все блоки
        /// Таким образом в памяти весят данные только по нескольким готовым блокам и всегда есть запас информации ожидающей записи
        /// Бутылочным горлышком является скорость записи на HDD, что видно в диспетчере задач
        /// </summary>
        /// <param name="sender">Объект завершивший обработку блока данных</param>
        /// <param name="e">Не используется</param>
        private void BlockThread_Compleated(dynamic sender, EventArgs e)
        {
            //Блокируем создание новых задач другими потоками (для соблюдения порядка обработки очереди)
            lock (_NewBlockThreadLoker)
            {
                if (sender.ErrorStatus)
                    ErrorStatus = true;
                if (ErrorStatus)
                {
                    if (Environment.ExitCode == 0)
                        Console.WriteLine("Для выхода из программы нажмите Enter");
                    Environment.ExitCode = 1;
                    return;
                }

                //Если очередь обработки блоков не завершена, то запускаем обработку следующего блока данных
                if (_BlockToRead < _BlockCount)
                    StartNewBlockThread();
            }

            //Блокируем обработку списка готовых блоков другими потоками
            lock (_CompleatedBTDict)
            {
                //Сохраняем объект завершивший обработку блока данных
                _CompleatedBTDict.Add(sender.Id, sender);

                //Блоки записываем в файл, соблюдая их очерёдность
                //Если текущий блок следующий в очереди записи, то начинаем запись
                if (_BlockToWrite == sender.Id)
                {
                    //Запись в файл продолжаем пока в списке готовых блоков не найдём следующий в очереди записи блок
                    while (_CompleatedBTDict.TryGetValue(_BlockToWrite, out ABlockThread bt))
                    {
                        Write(bt);

                        _BlockToWrite++;

                        //Очищаем данные записанного блока
                        _CompleatedBTDict.Remove(bt.Id);
                        bt.Compleated -= BlockThread_Compleated;
                        bt.Dispose();
                    }

                    //Обработка условия завершения списка всех задач
                    if (_BlockToWrite == _BlockCount)
                    {
                        _TargetFileStream.Close();
                        _TargetFileStream.Dispose();
                        _Stopwatch.Stop();
                        Console.WriteLine($"Время выполнения: {_Stopwatch.Elapsed}");
                        Console.WriteLine("Для выхода из программы нажмите Enter");
                        waitHandler.Set();
                    }
                }
            }
        }

        /// <summary>
        /// Записывает преобразованные блоки в йал
        /// </summary>
        /// <param name="pt">Объект с данными</param>
        private void Write(ABlockThread pt)
        {
            try
            {
                Console.WriteLine($"Запись [{pt.Id + 1}/{_BlockCount}]...");

                if (Mode == CompressionMode.Compress)
                    BitConverter.GetBytes(pt.Data.Length).CopyTo(pt.Data, 4);

                _TargetFileStream.Write(pt.Data, 0, pt.Data.Length);

                Console.WriteLine($"Запись [{pt.Id + 1}/{_BlockCount}] - OK");
            }
            catch (Exception exp)
            {
                Console.WriteLine($"Ошибка записи данных в файл: блок \"{pt.Id}\" ({exp.Message})");
                ErrorStatus = true;
            }
        }
    }
}
