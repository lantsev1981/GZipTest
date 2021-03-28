using System;
using System.IO;
using System.IO.Compression;

namespace  GZipTestLibrary
{
    /// <summary>
    /// Описывает базовый набор методов и свойств которые должна реализовывать GZip фабрика потоковой обработки данных
    /// </summary>
    public abstract class AGZipThreadFactory
    {
        /// <summary>
        /// Статус выполнения операций
        /// </summary>
        public bool ErrorStatus { get; protected set; }

        /// <summary>
        /// Режим работы архивация/разархивация
        /// </summary>
        public CompressionMode Mode { get; protected set; }

        /// <summary>
        /// Базовая информация об исходном файле
        /// </summary>
        public FileInfo SourceFile { get; protected set; }

        /// <summary>
        /// Размер максимального блока
        /// Для сжатия 1МБ, для разархивации исходя из метрики файла
        /// (при необходимости можно вывести в параметры создания фабрики)
        /// </summary>
        public readonly int MaxBlockSize = 1024 * 1024;


        /// <summary>
        /// Запускает поток обработки данных
        /// </summary>
        public abstract void Start(CompressionMode mode, string sourceFileName, string targetFileName);

        //public EventHandler<ProgressMessage> ProgressMessage;
    }

    /// <summary>
    /// Описывает базовый набор методов и свойств
    /// которым должен соответствовать компонент потоковой обработки блоков данных для использования в фабрике
    /// </summary>
    public abstract class ABlockThread : IDisposable
    {
        /// <summary>
        /// Статус выполнения операции
        /// </summary>
        public bool ErrorStatus { get; protected set; }

        /// <summary>
        /// Управляющая фабрика
        /// </summary>
        protected AGZipThreadFactory _Factory { get; private set; }

        /// <summary>
        /// Позиция блока в исходном файле
        /// </summary>
        protected long _Position { get; private set; }

        /// <summary>
        /// Идентификатор блока
        /// </summary>
        public long Id { get; private set; }

        /// <summary>
        /// Данные - результат выполнения операции
        /// </summary>
        public byte[] Data { get; protected set; }

        /// <summary>
        /// Событие завершение операции
        /// </summary>
        internal EventHandler Compleated;

        /// <summary>
        /// Передача параметров блока потоковой обработки данных
        /// (не знаю как это сделать через конструктор,
        /// с учётом абстрактного класса и типизации фабрики для возможности замены этого компонента системы на другой)
        /// </summary>
        /// <param name="factory">Управляющая фабрика</param>
        /// <param name="Id">Идентификатор блока</param>
        /// <param name="position">Позиция блока в исходном файле</param>
        public virtual void SetSettings(AGZipThreadFactory factory, long id, long position)
        {
            Id = id;
            _Factory = factory;
            _Position = position;
            Data = null;
        }

        /// <summary>
        /// Реализация механизма для освобождения неуправляемых ресурсов
        /// </summary>
        public virtual void Dispose()
        {
            _Factory = null;
            Data = null;
            Compleated = null;
        }

        /// <summary>
        /// Запускает поток обработки данных
        /// </summary>
        public abstract void Start();
    }

    /*public class ProgressMessage : EventArgs
    {
        string MessageProgress { get; set; }
    }*/
}
