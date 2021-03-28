using GZipTestLibrary;
using System;
using System.IO;
using System.IO.Compression;

namespace GZipTest
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Разработчик Сергей Ланцев");
            Console.WriteLine("Телефон (whatsapp, telegramm): +79253696558 , email: lantsev1981@gmail.com");
            Console.WriteLine("Логин в FB, VK, Skype, GitHub и других сервисах: lantsev1981");
            Console.WriteLine("Тестовое задание - многопоточный архиватор GZip");
            Console.WriteLine();

            //Проверяем входные параметры
            if (args.Length != 3
                || (args[0] != "compress" && args[0] != "decompress")
                || string.IsNullOrWhiteSpace(args[1])
                || string.IsNullOrWhiteSpace(args[2]))
            {
                Console.WriteLine("Неверно заданы параметры вызова! Укажите параметры в формате: compress/decompress [имя исходного файла] [имя результирующего файла]");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"Будет произведена {(args[0] == "compress" ? "архивация" : "разархивация")} файла {args[1]} в файл {args[2]}");
            Console.WriteLine("Для продолжения нажмите Enter, для отмены операции нажмите Ctrl+C");
            Console.ReadLine();

            //Создаём фабрику и запускаем процесс обработки данных
            var gZipTF = new GZipThreadFactory<BlockThread>();
            gZipTF.Start(args[0] == "compress" ? CompressionMode.Compress : CompressionMode.Decompress, args[1], args[2]);

            if (gZipTF.ErrorStatus)
            {
                Console.WriteLine("Операция завершена с ошибкой");
                Console.ReadLine();
                Environment.Exit(1);
            }
            else
            {
                Console.WriteLine("Операция успешно завершена");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }
    }
}