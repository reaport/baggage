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
    private double VehicleCapacity = 500.0;
    
    // Скорость машины (в м/с)
    private const double SpeedCar = 8;

    // Словарь для хранения машин
    private static readonly Dictionary<string, string> vehicleNodeMapping = new Dictionary<string, string>
    {
        ["baggage1"] = "node1",
        ["baggage2"] = "node2",
        ["baggage3"] = "node3"
    };
        //new();

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

    private async Task<RegisterVehicleResponse?> RegisterVehicleAsync(string vehicleType)
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

    public class MoveResponse
    {
        public double Distance { get; set; }
    }

    private async Task<double?> GetPermissionAsync(string vehicleId, string from, string to)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Создаем тело запроса
        var jsonData = new
        {
            vehicleId,
            vehicleType = "baggage",
            from,
            to
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await client.PostAsync("/move", content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            MoveResponse moveResponse = JsonConvert.DeserializeObject<MoveResponse>(result);
            Console.WriteLine($"Distance: {moveResponse.Distance}");
            return moveResponse.Distance;
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Обработка ошибки 400 (Неверные данные)
            Console.WriteLine($"Ошибка 400: Неверный запрос.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // Обработка ошибки 403 (Запрещено)
            Console.WriteLine("Ошибка 403: Перемещение запрещено. У вас нет прав для выполнения этого действия.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            //Обработка ошибки 404 
            Console.WriteLine("Один из узлов не найден.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // Обработка ошибки 409 (Узел занят)
            Console.WriteLine($"Узел сейчас занят, попробуйте позже");
            // Добавляем 1-секундную задержку перед повторным вызовом функции
            await Task.Delay(1000);
            // Повторный вызов функции с теми же параметрами
            return await GetPermissionAsync(vehicleId, from, to);
        }
        else
        {
            // Обработка других ошибок
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Произошла неизвестная ошибка: {response.ReasonPhrase}, содержимое: {errorContent}");
        }

        return null; // Если запрос не удался, возвращаем null
    }

    private static readonly object lockObject = new object(); // Объект для блокировки
    private async void informAboutArrivalAsync(string vehicleId, string nodeId)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Создаем тело запроса
        var jsonData = new
        {
            vehicleId,
            vehicleType = "baggage",
            nodeId
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await client.PostAsync("/arrived", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Уведомление успешно обработано");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Обработка ошибки 400 (Неверные данные)
            Console.WriteLine($"Ошибка 400: Неверный запрос.");
        }
    }

    [HttpPost("load")]
    public async Task<ActionResult<BaggageResponse>> LoadBaggage([FromBody] BaggageLoadRequest request)
    {
        // Проверка на корректный запрос
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            return BadRequest(new ErrorResponse { Error = "Неверный запрос" });
        }

        // Если свободные машины есть
        bool waiting = false;

        // Считаем количество необходимых для перевозки машин
        int countCars = (int)Math.Ceiling((double)request.BaggageWeight / VehicleCapacity);

        var tasks = new List<Task>();

        for (int i = 0; i < countCars; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                Console.WriteLine($"Отправлена машина");

                string availableVehicleId = null;
                string availableVehiclePlace = null;

                // Чтобы не взять одну и ту же машину
                lock (lockObject)
                {

                    // Ищем свободную машину
                    var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "в пути");

                    // Проверяем, что такая машина найдена
                    if (foundVehicle.Key != null)
                    {
                        availableVehicleId = foundVehicle.Key;
                        availableVehiclePlace = foundVehicle.Value;
                        vehicleNodeMapping[foundVehicle.Key] = "в пути";
                    }
                }
                if (availableVehicleId != null)
                {
                    Console.WriteLine($"Найдена доступная машина: ID = {availableVehicleId}, местоположение = {availableVehiclePlace}");

                    bool back = false;

                    for (int k = 0; k < 2; k++)
                    {
                        // Получаем маршрут
                        //Дефолт для проверки
                        string[] routePoints = ["node1", "node2", "node3"];
                        // Реальный запрос
                        //var routePoints = await GetRouteAsync(availableVehiclePlace, request.AircraftCoordinates);

                        if (routePoints != null)
                        {
                            // Обработка полученных данных
                            Console.WriteLine("Маршрут:");
                            foreach (var point in routePoints)
                            {
                                Console.WriteLine($"ID точки: {point}");
                            }

                            // Движение по маршруту
                            for (int j = 0; j < routePoints.Length - 1; j++)
                            {
                                // Запрашиваем разрешение на передвижение (повторно, если необходимо тоже)
                                // Дефолт
                                double distanse = 100;
                                // Реальный запрос
                                //var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                // ИЛИ НОЛЬ?
                                if (distanse != null)
                                {
                                    // Считаем время в пути
                                    int time = (int)Math.Ceiling(distanse / SpeedCar);
                                    await Task.Delay(time * 1000);

                                    // Уведомляем о прибытии
                                    // Реальный запрос
                                    //informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                }
                            }

                            //Сообщаем, что прибыли на финальную точку ОРКЕСТРАТОРУ
                            // Если двигались к самолёту - совершаем загрузку
                            if (back == false)
                            {
                                // Выполняем загрузку
                                Console.WriteLine("Загрузка выполняется");
                                await Task.Delay(5000);
                                back = true;
                            }
                            // Если двигались к гаражу, помечаем машину как свободную
                            else
                            {
                                Console.WriteLine($"Машина {availableVehicleId} прибыла в гараж");
                                vehicleNodeMapping[availableVehicleId] = availableVehiclePlace;
                            }

                        }
                        else
                        {
                            Console.WriteLine("Не удалось получить маршрут.");
                        }
                    }
                }
                else
                {
                    waiting = false;
                    Console.WriteLine("Регистрация");
                    // Регистрация новой машины
                    // Реальный запрос
                    /*var response = await RegisterVehicleAsync("baggage");

                    if (response != null)
                    {
                        Console.WriteLine($"Зарегистрирована машина: Node ID: {response.NodeId}, Vehicle ID: {response.VehicleId}");
                        vehicleNodeMapping.Add(response.VehicleId, response.NodeId);
                        i--;
                    }
                    else
                    {
                        Console.WriteLine("Запрос завершился неуспешно, данные отсутствуют.");
                    }*/
                }
            }));
        }
        await Task.WhenAll(tasks);
        return Ok(new BaggageResponse { Waiting = waiting });
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
