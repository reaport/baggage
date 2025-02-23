using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

[ApiController]
public class BaggageController : ControllerBase
{
    // ОГРАНИЧЕНИЕ НА КОЛИЧЕСТВО МАШИН

    private static readonly HttpClient client = new();

    // Вместимость одной машины
    private const double VehicleCapacity = 500.0;

    // Словарь для хранения машин
    private static readonly Dictionary<string, string> vehicleNodeMapping = new();

    public class BaggageLoadRequest
    {
        public string AircraftId { get; set; }
        public double BaggageWeight { get; set; }
        public string AircraftCoordinates { get; set; }
    }

    public class BaggageUploadRequest
    {
        public string AircraftId { get; set; }
        public double BaggageWeight { get; set; }
        public string AircraftCoordinates { get; set; }
    }

    public class BaggageResponse
    {
        public bool Waiting { get; set; }
    }

    public class ErrorResponse
    {
        public string Error { get; set; }
    }

    public class RegisterVehicleResponse
    {
        public string NodeId { get; set; }
        public string VehicleId { get; set; }
    }

    public async Task<RegisterVehicleResponse?> RegisterVehicleAsync(string vehicleType)
    {
        // Формирование URL для запроса
        string url = $"/register-vehicle/{vehicleType}";

        // Отправка POST-запроса
        var response = await client.PostAsync(url, null);

        if (response.IsSuccessStatusCode)
        {
            // Чтение ответа, если запрос успешен (код 200)
            var result = await response.Content.ReadFromJsonAsync<RegisterVehicleResponse>();
            return result; // Возвращаем результат
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Обработка ошибки Bad Request (код 400)
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error 400: Неверные данные запроса. {error}");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // Обработка ошибки Forbidden (код 403)
            Console.WriteLine("Error 403: Нет свободного узла.");
        }
        else
        {
            // Обработка других ошибок
            Console.WriteLine($"Произошла ошибка: {response.StatusCode}");
        }

        return null; // Если запрос не удался, возвращаем null
    }

    private async Task<string[]?> GetRouteAsync(string from, string to)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Создаем тело запроса
        var jsonData = new
        {
            from,
            to,
            type = "baggage"
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await client.PostAsync("/route", content);

        if (response.IsSuccessStatusCode)
        {
            // Чтение и десериализация ответа
            var result = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<string[]>(result);
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Console.WriteLine("Маршрут не найден.");
        }
        else
        {
            // Обработка других ошибок
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Произошла ошибка: {response.ReasonPhrase}, содержимое: {errorContent}");
        }

        return null; // Если запрос не удался, возвращаем null
    }
    [HttpPost("load")]
    public async Task<ActionResult<BaggageResponse>> LoadBaggage([FromBody] BaggageLoadRequest request)
    {
        // Проверка на корректный запрос
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            return BadRequest(new ErrorResponse { Error = "Неверный запрос" });
        }

        // Считаем количество необходимых для перевозки машин
        int countCars = (int)Math.Ceiling((double)request.BaggageWeight / VehicleCapacity);

        for (int i = 0; i < countCars; i++)
        {
            // Ищем свободную машину
            var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "в пути");

            // Проверяем, что такая машина найдена
            if (foundVehicle.Key != null)
            {
                string availableVehicleId = foundVehicle.Key;
                string availableVehiclePlace = foundVehicle.Value;

                Console.WriteLine($"Найдена доступная машина: ID = {availableVehicleId}, местоположение = {availableVehiclePlace}");

                //Дефолт для проверки
                string[] routePoints = ["node1", "node2", "node3"];

                // Получаем маршрут
                // Реальный запрос
                //var routePoints = await GetRouteAsync(availableVehiclePlace, request.AircraftCoordinates);
                if (routePoints != null)
                {
                    // Обработка полученных данных
                    foreach (var point in routePoints)
                    {
                        Console.WriteLine("Маршрут:");
                        Console.WriteLine($"ID точки: {point}");
                        // В цикле:
                        // Запрашиваем разрешение на передвижение (повторно, если необходимо тоже)
                        // Считаем время передвижения
                        // Уведомляем о прибытии

                        //Сообщаем, что прибыли на финальную точку
                        //Возвращение в гараж
                    }

                }
                else
                {
                    Console.WriteLine("Не удалось получить маршрут.");
                }
            }
            else
            {
                // Регистрация новой машины
                string vehicleType = "baggage";
                var response = await RegisterVehicleAsync(vehicleType);

                if (response != null)
                {
                    Console.WriteLine($"Node ID: {response.NodeId}, Vehicle ID: {response.VehicleId}");
                    // Здесь логика
                }
                else
                {
                    Console.WriteLine("Запрос завершился неуспешно, данные отсутствуют.");
                }
            }











            // Здесь должна быть логика обработки загрузки


            // Если свободных машин нет
            bool waiting = false; // Заменить на логику
            return Ok(new BaggageResponse { Waiting = waiting });
        }
        return Ok(new BaggageResponse { Waiting = true });
    }

    [HttpPost("upload")]
    public ActionResult<BaggageResponse> UploadBaggage([FromBody] BaggageUploadRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            return BadRequest(new ErrorResponse { Error = "Неверный запрос" });
        }

        // Здесь должна быть логика обработки выгрузки


        // Если свободных машин нет
        bool waiting = false; // Заменить
        return Ok(new BaggageResponse { Waiting = waiting });
    }
}
