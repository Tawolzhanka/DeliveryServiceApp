using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DeliveryServiceApp
{
    class Order
    {
        public int OrderId { get; set; }
        public double Weight { get; set; }
        public string District { get; set; }
        public DateTime DeliveryTime { get; set; }
    }

    class Program
    {
        private static string logFilePath;
        private static string resultFilePath;
        private static List<string> validDistricts = new List<string>();

        static void Main()
        {
            LoadConfiguration();

            List<Order> orders = LoadOrders("orders.csv"); // Загружает заказы из CSV файла
            validDistricts = ExtractUniqueDistricts(orders); // Извлекает уникальные районы из заказов

            string cityDistrict = PromptUserForDistrict(); // Запрашивает у пользователя район

            // Запрашиваем у пользователя время начала и окончания
            DateTime startDeliveryDateTime = PromptUserForStartDeliveryTime(); // Запрашиваем время начала
            DateTime endDeliveryDateTime = PromptUserForEndDeliveryTime(); // Запрашиваем время окончания

            InitializeDirectories(); // Создаёт необходимые директории для логов и результатов

            Log($"Программа запущена. Фильтрация по району: {cityDistrict}, время с: {startDeliveryDateTime}, по: {endDeliveryDateTime}");

            var filteredOrders = FilterOrders(orders, cityDistrict, startDeliveryDateTime, endDeliveryDateTime); // Фильтрует заказы по району и времени доставки

            SaveFilteredOrders(filteredOrders, startDeliveryDateTime); // Сохраняет отфильтрованные заказы в файл
        }

        private static List<string> ExtractUniqueDistricts(List<Order> orders)
        {
            return orders.Select(o => o.District)
                         .Distinct()
                         .ToList(); // Извлекает уникальные районы из списка заказов
        }

        private static string PromptUserForDistrict()
        {
            while (true)
            {
                string district = PromptUserForInput("Введите район для фильтрации:");
                if (IsValidDistrict(district.Trim())) // Удаляет пробелы при проверке
                {
                    return district.Trim(); // Возвращает валидный район
                }
                Console.WriteLine("Некорректное название района. Пожалуйста, введите один из следующих районов:");
                Console.WriteLine(string.Join(", ", validDistricts));
            }
        }

        private static bool IsValidDistrict(string district)
        {
            bool isValid = validDistricts.Contains(district, StringComparer.OrdinalIgnoreCase);
            if (!isValid)
            {
                Log($"Некорректный район: {district}"); // Логирует некорректный район
            }
            return isValid;
        }

        private static DateTime PromptUserForStartDeliveryTime()
        {
            while (true)
            {
                string input = PromptUserForInput("Введите время начала фильтрации в формате 'yyyy-MM-dd HH:mm:ss':");
                if (DateTime.TryParseExact(input, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime deliveryTime))
                {
                    return deliveryTime;
                }
                Console.WriteLine("Некорректный формат времени. Используйте формат 'yyyy-MM-dd HH:mm:ss'.");
            }
        }

        private static DateTime PromptUserForEndDeliveryTime()
        {
            while (true)
            {
                string input = PromptUserForInput("Введите время окончания фильтрации в формате 'yyyy-MM-dd HH:mm:ss':");
                if (DateTime.TryParseExact(input, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime deliveryTime))
                {
                    return deliveryTime;
                }
                Console.WriteLine("Некорректный формат времени. Используйте формат 'yyyy-MM-dd HH:mm:ss'.");
            }
        }

        private static void LoadConfiguration()
        {
            var configText = File.ReadAllText("appsettings.json");
            var config = JObject.Parse(configText);
            logFilePath = config["FilePaths"]["LogFilePath"].ToString(); // Загружает путь к файлу логов
            resultFilePath = config["FilePaths"]["ResultFilePath"].ToString(); // Загружает путь к файлу результатов
        }

        private static string PromptUserForInput(string message)
        {
            Console.WriteLine(message);
            return Console.ReadLine().Trim(); // Запрашивает ввод от пользователя и убирает пробелы
        }

        private static void InitializeDirectories()
        {
            Directory.CreateDirectory("logs"); // Создаёт директорию для логов
            Directory.CreateDirectory("results"); // Создаёт директорию для результатов
        }

        private static List<Order> LoadOrders(string filePath)
        {
            try
            {
                var orders = new List<Order>();
                var failedOrders = new List<string>(); // Список для хранения неудачных заказов
                foreach (var line in File.ReadLines(filePath).Skip(1)) // Пропускает заголовок
                {
                    if (TryParseOrder(line, out Order order))
                    {
                        orders.Add(order); // Добавляет успешно загруженный заказ
                        Log($"Загружен заказ: {order.OrderId}, Район: {order.District}"); // Логирует загружаемый заказ
                    }
                    else
                    {
                        failedOrders.Add(line); // Сохраняет строку с ошибкой
                    }
                }

                // Логирование результатов загрузки
                if (failedOrders.Count == 0)
                {
                    Log($"Все {orders.Count} заказов успешно загружены."); // Логирует успех загрузки
                }
                else
                {
                    foreach (var failedOrder in failedOrders)
                    {
                        Log($"Не удалось загрузить заказ из строки: {failedOrder}"); // Логирует ошибки загрузки
                    }
                }

                return orders; // Возвращает список загруженных заказов
            }
            catch (FileNotFoundException ex)
            {
                Log($"Файл не найден: {ex.Message}"); // Логирует ошибку при отсутствии файла
                return new List<Order>();
            }
            catch (Exception ex)
            {
                Log($"Ошибка при чтении CSV файла: {ex.Message}"); // Логирует ошибки чтения  файла
                return new List<Order>();
            }
        }

        private static bool TryParseOrder(string line, out Order order)
        {
            order = null;
            var parts = line.Split(',');
            if (parts.Length != 4)
            {
                Log($"Некорректный формат строки: {line}"); // Логирует некорректный формат
                return false;
            }

            int orderId;
            double weight = 0;
            DateTime deliveryTime;

            if (int.TryParse(parts[0], out orderId) &&
                double.TryParse(parts[1], out weight) &&
                DateTime.TryParseExact(parts[3], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out deliveryTime))
            {
                order = new Order
                {
                    OrderId = orderId,
                    Weight = weight,
                    District = parts[2].Trim(),
                    DeliveryTime = deliveryTime
                };
                return true;
            }

            if (DateTime.TryParseExact(parts[3], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out deliveryTime))
            {
                order = new Order
                {
                    OrderId = orderId,
                    Weight = weight,
                    District = parts[2].Trim(),
                    DeliveryTime = deliveryTime
                };
                return true;
            }

            Log($"Не удалось распарсить заказ из строки: {line}"); // Логирует ошибку парсинга
            return false;
        }

        private static List<Order> FilterOrders(List<Order> orders, string cityDistrict, DateTime startDeliveryDateTime, DateTime endDeliveryDateTime)
        {
            // Фильтруем заказы по району и времени доставки
            return orders.Where(o => o.District.Equals(cityDistrict, StringComparison.OrdinalIgnoreCase)
                                     && o.DeliveryTime >= startDeliveryDateTime
                                     && o.DeliveryTime <= endDeliveryDateTime).ToList();
        }

        private static void SaveFilteredOrders(List<Order> filteredOrders, DateTime startDeliveryDateTime)
        {
            try
            {
                using (var writer = new StreamWriter(resultFilePath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("OrderId,Weight,District,DeliveryTime");
                    // Записываем заказы, которые находятся в пределах 30 минут с момента первого заказа
                    var firstOrderTime = filteredOrders.OrderBy(o => o.DeliveryTime).FirstOrDefault()?.DeliveryTime;
                    if (firstOrderTime.HasValue)
                    {
                        var endOfFilteringPeriod = firstOrderTime.Value.AddMinutes(30);
                        var ordersInTimeWindow = filteredOrders.Where(o => o.DeliveryTime <= endOfFilteringPeriod).ToList();

                        foreach (var order in ordersInTimeWindow)
                        {
                            writer.WriteLine($"{order.OrderId},{order.Weight},{order.District},{order.DeliveryTime:yyyy-MM-dd HH:mm:ss}"); // Записывает отфильтрованные заказы в файл
                        }
                    }
                }
                Log($"Отфильтровано {filteredOrders.Count} заказов, результат успешно записан в файл."); // Логирует успешное сохранение
            }
            catch (Exception ex)
            {
                Log($"Ошибка при записи в файл результата: {ex.Message}"); // Логирует ошибку записи
            }
        }

        static void Log(string message)
        {
            using (var writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}"); // Логирует сообщения с временной меткой
            }
        }
    }
}
