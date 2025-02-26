using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

[ApiController]
public class BaggageController : ControllerBase
{
    // Вместимость одной машины
    private static double VehicleCapacity = 500.0;

    // Скорость машины (в м/с)
    private const double SpeedCar = 8;

    // Словарь для хранения машин
    private static readonly Dictionary<string, string> vehicleNodeMapping = new Dictionary<string, string>
    {
        /*["baggage1"] = "node1",
        ["baggage2"] = "node2",
        ["baggage3"] = "node3"*/
    };
    //new();

    private static readonly Dictionary<string, Dictionary<string, string>> vehicleNodeMappingPlace = new Dictionary<string, Dictionary<string, string>>
    {
        /*["baggage1"] =  "airplane_parking_1": "airplane_parking_1_baggage_1",
    "airplane_parking_2": "airplane_parking_2_baggage_1",
        ["baggage2"] = "node2",
        ["baggage3"] = "node3"*/
    };

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
        public string garrageNodeId { get; set; }
        public string VehicleId { get; set; }
        public Dictionary<string, string> serviceSpots { get; set; }
    }

    private async Task<RegisterVehicleResponse?> RegisterVehicleAsync(string vehicleType)
    {
        HttpClient client = new();
        // Формирование URL для запроса
        string url = $"https://ground-control.reaport.ru/register-vehicle/{vehicleType}";

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
        HttpClient client = new();
        Console.WriteLine("Зашли в функцию");
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

        Console.WriteLine("Пытаюсь отправить пост запрос");
        // Отправляем POST-запрос
        var response = await client.PostAsync("https://ground-control.reaport.ru/route", content);
        Console.WriteLine("Пост запрос отправлен");

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
        HttpClient client = new();
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
        var response = await client.PostAsync("https://ground-control.reaport.ru/move", content);

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
            Console.WriteLine($"{vehicleId}: Ошибка 400 GetPermissionAsync: Неверный запрос. Машина просит разрешение попасть из узла {from} в узел {to}");
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
            Console.WriteLine($"{vehicleId}: Пытаюсь попасть из узла {from}. Узел {to} сейчас занят, попробуйте позже");
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

    private async Task informAboutArrivalAsync(string vehicleId, string nodeId)
    {
        HttpClient client = new();
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
        var response = await client.PostAsync("https://ground-control.reaport.ru/arrived", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Уведомление о прибытии успешно доставлено. Машина {vehicleId} прибыла в узел {nodeId}"); 
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Обработка ошибки 400 (Неверные данные)
            Console.WriteLine($"Ошибка 400 informAboutArrivalAsync: Неверный запрос. Машина {vehicleId} не прибыла в узел {nodeId}");
        }
    }

    private static readonly object lockObject = new object(); // Объект для блокировки

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

        // Проверяем, есть ли хотя бы одна свободная машина
        lock (lockObject)
        {
            var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "в пути");
            if (foundVehicle.Key == null)
            {
                waiting = true; // Если нет свободных машин
            }
        }

        var tasks = new List<Task>();

        for (int i = 0; i < countCars; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                string availableVehicleId = null;
                string availableVehiclePlace = null;
                string availableVehiclePlacePlane = null;

                // Чтобы не взять одну и ту же машину
                lock (lockObject)
                {
                    foreach (var innerPair in vehicleNodeMapping)
                    {
                        Console.WriteLine($"  0Ключ внутреннего словаря: {innerPair.Key}, Значение: {innerPair.Value}");
                    }
                    // Ищем свободную машину
                    var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "в пути");

                    // Проверяем, что такая машина найдена
                    if (foundVehicle.Key != null)
                    {
                        /*foreach (var innerPair in vehicleNodeMapping)
                        {
                            if (innerPair.Value != "в пути")
                            {
                                availableVehicleId = innerPair.Key;
                                availableVehiclePlace = innerPair.Value;
                            }
                        }*/
                        /*foreach (var outerPair in vehicleNodeMappingPlace)
                        {
                            Console.WriteLine($"Ключ внешнего словаря: {outerPair.Key}");
                            foreach (var innerPair in outerPair.Value)
                            {
                                Console.WriteLine($"  Ключ внутреннего словаря: {innerPair.Key}, Значение: {innerPair.Value}");
                            }
                        }*/
                        availableVehicleId = foundVehicle.Key;
                        availableVehiclePlace = foundVehicle.Value;
                        //Console.WriteLine($"Машина {response.VehicleId}, местоположение нужного самолёта: {response.serviceSpots[request.AircraftCoordinates]} добавлена в словарь");
                        
                        var innerDictionary = vehicleNodeMappingPlace[foundVehicle.Key];
                        availableVehiclePlacePlane = innerDictionary[request.AircraftCoordinates];
                        vehicleNodeMapping[foundVehicle.Key] = "в пути";
                        foreach (var innerPair in vehicleNodeMapping)
                        {
                            Console.WriteLine($"  1Ключ внутреннего словаря: {innerPair.Key}, Значение: {innerPair.Value}");
                        }
                    }
                }
                if (availableVehicleId != null)
                {
                    foreach (var innerPair in vehicleNodeMapping)
                    {
                        Console.WriteLine($"2  Ключ внутреннего словаря: {innerPair.Key}, Значение: {innerPair.Value}");
                    }
                    Console.WriteLine($"Найдена доступная машина: ID = {availableVehicleId}, местоположение = {availableVehiclePlace}");
                    // Получаем маршрут
                    //Дефолт для проверки
                    //string[] routePoints = ["node1", "node2", "node3"];
                    // Реальный запрос
                    var routePoints = await GetRouteAsync(availableVehiclePlace, availableVehiclePlacePlane);

                    if (routePoints != null)
                    {
                        // Обработка полученных данных
                        Console.WriteLine($"Маршрут для {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID точки: {point}");
                        }

                        // Движение по маршруту
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // Запрашиваем разрешение на передвижение (повторно, если необходимо тоже)
                            // Дефолт
                            //double distanse = 100;
                            // Реальный запрос
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                            // ИЛИ НОЛЬ?
                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                // Считаем время в пути
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                // Уведомляем о прибытии
                                // Реальный запрос

                            }
                        }

                        //Сообщаем, что прибыли на финальную точку ОРКЕСТРАТОРУ
                        // Если двигались к самолёту - совершаем загрузку

                        // Выполняем загрузку
                        Console.WriteLine("Загрузка выполняется");
                        await Task.Delay(5000);
                    }
                    else
                    {
                        Console.WriteLine("Не удалось получить маршрут.");
                    }

                    // Получаем маршрут, чтобы поехать к гаражу
                    routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                    if (routePoints != null)
                    {
                        // Обработка полученных данных
                        Console.WriteLine($"Маршрут для {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID точки: {point}");
                        }

                        // Движение по маршруту
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // Запрашиваем разрешение на передвижение (повторно, если необходимо тоже)
                            // Дефолт
                            //double distanse = 100;
                            // Реальный запрос
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                            // ИЛИ НОЛЬ?
                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                // Считаем время в пути
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                // Уведомляем о прибытии
                                // Реальный запрос
                            }
                        }
                        // Помечаем машину как свободную

                        Console.WriteLine($"Машина {availableVehicleId} прибыла в гараж");
                        vehicleNodeMapping[availableVehicleId] = availableVehiclePlace;
                    }
                    else
                    {
                        Console.WriteLine("Не удалось получить маршрут.");
                    }
                }
                else
                {
                    // Регистрация новой машины и отправление её к самолёту
                    // Реальный запрос
                    Console.WriteLine("Попытка зайти в функцию зарегистрировать машину");
                    var response = await RegisterVehicleAsync("baggage");
                    Console.WriteLine("Вышли из функции регистрации");

                    if (response != null)
                    {
                        Console.WriteLine($"Зарегистрирована машина: Node ID: {response.garrageNodeId}, Vehicle ID: {response.VehicleId}");
                        vehicleNodeMapping.Add(response.VehicleId, "в пути");
                        Console.WriteLine($"Машина {response.VehicleId}, местоположение: {response.garrageNodeId} добавлена в словарь");
                        vehicleNodeMappingPlace.Add(response.VehicleId, response.serviceSpots);
                        Console.WriteLine($"Машина {response.VehicleId}, местоположение нужного самолёта: {response.serviceSpots[request.AircraftCoordinates]} добавлена в словарь");

                        availableVehicleId = response.VehicleId;
                        availableVehiclePlace = response.garrageNodeId;
                        //var innerDictionary = vehicleNodeMappingPlace[response.garrageNodeId];
                        availableVehiclePlacePlane = response.serviceSpots[request.AircraftCoordinates];
                        Console.WriteLine($"Парковка у самолёта: {availableVehiclePlacePlane}");

                        // Получаем маршрут
                        //Дефолт для проверки
                        //string[] routePoints = ["node1", "node2", "node3"];
                        // Реальный запрос 
                        Console.WriteLine($"Попытка зайти в функцию получить маршрут от {availableVehiclePlace} до {availableVehiclePlacePlane}");
                        var routePoints = await GetRouteAsync(availableVehiclePlace, availableVehiclePlacePlane);

                            if (routePoints != null)
                            {
                                // Обработка полученных данных
                                Console.WriteLine($"Маршрут для {availableVehicleId}:");
                                foreach (var point in routePoints)
                                {
                                    Console.WriteLine($"ID точки: {point}");
                                }

                                // Движение по маршруту
                                for (int j = 0; j < routePoints.Length - 1; j++)
                                {
                                    // Запрашиваем разрешение на передвижение (повторно, если необходимо тоже)
                                    // Дефолт
                                    //double distanse = 100;
                                    // Реальный запрос
                                    var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                    // ИЛИ НОЛЬ?
                                    if (distanse != null)
                                    {
                                        Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                        // Считаем время в пути
                                        int time = (int)Math.Ceiling((double)distanse / SpeedCar);
                                    // Поменять
                                        await Task.Delay(time * 100);

                                        await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                        // Уведомляем о прибытии
                                        // Реальный запрос

                                    }
                                }

                                //Сообщаем, что прибыли на финальную точку ОРКЕСТРАТОРУ
                                // Если двигались к самолёту - совершаем загрузку

                                // Выполняем загрузку
                                Console.WriteLine("Загрузка выполняется");
                                await Task.Delay(500);
                            }
                            else
                            {
                                Console.WriteLine("Не удалось получить маршрут.");
                            }

                            // Получаем маршрут, чтобы поехать к гаражу
                            routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                            if (routePoints != null)
                            {
                                // Обработка полученных данных
                                Console.WriteLine($"Маршрут для {availableVehicleId}:");
                                foreach (var point in routePoints)
                                {
                                    Console.WriteLine($"ID точки: {point}");
                                }

                                // Движение по маршруту
                                for (int j = 0; j < routePoints.Length - 1; j++)
                                {
                                    // Запрашиваем разрешение на передвижение (повторно, если необходимо тоже)
                                    // Дефолт
                                    //double distanse = 100;
                                    // Реальный запрос
                                    var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                    // ИЛИ НОЛЬ?
                                    if (distanse != null)
                                    {
                                        Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                        // Считаем время в пути
                                        int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                        await Task.Delay(time * 100);

                                        await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                        // Уведомляем о прибытии
                                        // Реальный запрос
                                    }
                                }
                                // Помечаем машину как свободную

                                Console.WriteLine($"Машина {availableVehicleId} прибыла в гараж");
                                vehicleNodeMapping[availableVehicleId] = availableVehiclePlace;
                            }
                            else
                            {
                                Console.WriteLine("Не удалось получить маршрут.");
                            }
                        
                    }
                    else
                    {
                        Console.WriteLine("Запрос завершился неуспешно, данные отсутствуют.");
                    }
                }
            }));
        }
        Task.WhenAll(tasks);
        return Ok(new BaggageResponse { Waiting = waiting });
    }

    [HttpPost("upload")]
    public ActionResult<BaggageResponse> UploadBaggage([FromBody] BaggageUploadRequest request)
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

        // Проверяем, есть ли хотя бы одна свободная машина
        lock (lockObject)
        {
            var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "в пути");
            if (foundVehicle.Key == null)
            {
                waiting = true; // Если нет свободных машин
            }
        }

        var tasks = new List<Task>();

        for (int i = 0; i < countCars; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
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
                                Console.WriteLine("Разгрузка выполняется");
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
                    Console.WriteLine("Регистрация");
                    // Регистрация новой машины и отправление её к самолёту
                    // Реальный запрос
                    /*var response = await RegisterVehicleAsync("baggage");

                    if (response != null)
                    {
                        Console.WriteLine($"Зарегистрирована машина: Node ID: {response.NodeId}, Vehicle ID: {response.VehicleId}");
                        vehicleNodeMapping.Add(response.VehicleId, response.NodeId);

                        availableVehicleId = response.VehicleId;
                        availableVehiclePlace = response.NodeId;

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
                                    Console.WriteLine("Разгрузка выполняется");
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
                        Console.WriteLine("Запрос завершился неуспешно, данные отсутствуют.");
                    }*/
                }
            }));
        }
        Task.WhenAll(tasks);
        return Ok(new BaggageResponse { Waiting = waiting });
    }



    private static async Task HandleSetCapacity(HttpContext context)
    {
        var request = await context.Request.ReadFromJsonAsync<CapacityRequest>();
        if (request != null)
        {
            VehicleCapacity = request.Capacity;
            await context.Response.WriteAsync("OK");
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }

    public class CapacityRequest
    {
        public int Capacity { get; set; }
    }

}

// Добавить ручки Никиты (жду Никиту)
// Протестировать с ребятами (жду Никиту)
// Сделать html страничку 
// Почистить код
// Написать документацию
