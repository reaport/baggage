using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

[ApiController]
public class BaggageController : ControllerBase
{
    public class VehicleCapacity
    {
        public double Capacity { get; set; }
    }
    // Вместимость одной машины
    private static VehicleCapacity _vehicleCapacity = new VehicleCapacity { Capacity = 3000 };

    // Скорость машины 
    private const double SpeedCar = 25;

    // Словарь для хранения машин и их местоположения
    private static readonly Dictionary<string, string> vehicleNodeMapping = new Dictionary<string, string>
    {
    };

    // Словарь для хранения парковок у самолёта
    private static readonly Dictionary<string, Dictionary<string, string>> vehicleNodeMappingPlace = new Dictionary<string, Dictionary<string, string>>
    {
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
        var response = await client.PostAsync("https://ground-control.reaport.ru/route", content);

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

    private async Task informAboutStartLoading(string aircraft_id)
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Создаем тело запроса
        var jsonData = new
        {
            aircraft_id
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос 
        var response = await client.PostAsync("https://orchestrator.reaport.ru/baggage/uploading/start", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Уведомление о начале загрузки самолета {aircraft_id} успешно доставлено.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            // Обработка ошибки Unprocessable Entity (код 422)
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error 422: Невозможно обработать запрос. {error}");
        }
    }

    private async Task informAboutStartUploading(string aircraft_id)
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Создаем тело запроса
        var jsonData = new
        {
            aircraft_id
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос 
        var response = await client.PostAsync("https://orchestrator.reaport.ru/baggage/unloading/start", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Уведомление о начале разгрузки самолета {aircraft_id} успешно доставлено.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            // Обработка ошибки Unprocessable Entity (код 422)
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error 422: Невозможно обработать запрос. {error}");
        }
    }

    private async Task informAboutFinishLoading(string aircraft_id, double baggage_count)
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Создаем тело запроса
        var jsonData = new
        {
            aircraft_id,
            baggage_count
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос 
        var response = await client.PostAsync("https://orchestrator.reaport.ru/baggage/uploading/finish", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Уведомление о конце загрузки самолета {aircraft_id} успешно доставлено.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            // Обработка ошибки Unprocessable Entity (код 422)
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error 422: Невозможно обработать запрос. {error}");
        }
    }

    private async Task informAboutFinishUploading(string aircraft_id, double baggage_count)
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Создаем тело запроса
        var jsonData = new
        {
            aircraft_id,
            baggage_count
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await client.PostAsync("https://orchestrator.reaport.ru/baggage/unloading/finish", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Уведомление о конце разгрузки самолета {aircraft_id} успешно доставлено.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            // Обработка ошибки Unprocessable Entity (код 422)
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error 422: Невозможно обработать запрос. {error}");
        }
    }

    private static readonly object lockObject = new object(); // Объект для блокировки

    [HttpPost("load")]
    public async Task<ActionResult<BaggageResponse>> LoadBaggage([FromBody] BaggageLoadRequest request)
    {
        // Проверка на корректный запрос
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            Console.WriteLine(request.AircraftId, request.BaggageWeight, request.AircraftCoordinates);
            return BadRequest(new ErrorResponse { Error = "Неверный запрос" });
        }

        // Если свободные машины есть
        bool waiting = false;

        // Считаем количество необходимых для перевозки машин
        int countCars = (int)Math.Ceiling((double)request.BaggageWeight / _vehicleCapacity.Capacity);

        bool startLoad = false;

        int countCarsFinish = 1;

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
                    // Ищем свободную машину
                    var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "в пути");

                    // Проверяем, что такая машина найдена
                    if (foundVehicle.Key != null)
                    {
                        availableVehicleId = foundVehicle.Key;
                        availableVehiclePlace = foundVehicle.Value;
                        
                        var innerDictionary = vehicleNodeMappingPlace[foundVehicle.Key];
                        availableVehiclePlacePlane = innerDictionary[request.AircraftCoordinates];
                        vehicleNodeMapping[foundVehicle.Key] = "в пути";
                    }
                }
                if (availableVehicleId != null)
                {
                    Console.WriteLine($"Найдена доступная машина: ID = {availableVehicleId}, местоположение = {availableVehiclePlace}");
                    // Получаем маршрут
                    // Дефолт для проверки string[] routePoints = ["node1", "node2", "node3"];
                    var routePoints = await GetRouteAsync(availableVehiclePlace, availableVehiclePlacePlane);

                    if (routePoints != null)
                    {
                        Console.WriteLine($"Маршрут для {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID точки: {point}");
                        }

                        // Движение по маршруту
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // Запрашиваем разрешение на передвижение
                            // Дефолт double distanse = 100;
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);

                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                // Считаем время в пути
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                // Уведомляем о прибытии
                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                            }
                        }

                        string firstCar = "";

                        lock (lockObject)
                        {
                            if (startLoad == false)
                            {
                                firstCar = availableVehicleId;
                                startLoad = true;
                            }
                        }
                        if (firstCar == availableVehicleId)
                        {
                            // Сообщаем оркестратору о начале загрузки первой машины
                            Console.WriteLine($"Сообщаем оркестратору о начале загрузки первой машиной {availableVehicleId}");
                            await informAboutStartLoading(request.AircraftId);
                        }

                        // Выполняем загрузку
                        Console.WriteLine($"Загрузка выполняется машиной {availableVehicleId}");
                        await Task.Delay(5000);

                        string lastCar = "";

                        lock (lockObject)
                        {
                            if (countCarsFinish == countCars)
                            {
                                lastCar = availableVehicleId;
                            }
                            else countCarsFinish++;
                        }

                        if (lastCar == availableVehicleId)
                        {
                            // Сообщаем оркестратору об окончании загрузки последней машины
                            Console.WriteLine($"Сообщаем оркестратору об окончании загрузки последней машиной {availableVehicleId}");
                            await informAboutFinishLoading(request.AircraftId, request.BaggageWeight);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Не удалось получить маршрут.");
                    }

                    // Получаем маршрут, чтобы поехать к гаражу
                    routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                    if (routePoints != null)
                    {
                        Console.WriteLine($"Маршрут для {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID точки: {point}");
                        }

                        // Движение по маршруту
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // Запрашиваем разрешение на передвижение 
                            // Дефолт double distanse = 100;
                            // Реальный запрос
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);

                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                // Считаем время в пути
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                // Уведомляем о прибытии
                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
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
                    var response = await RegisterVehicleAsync("baggage");

                    if (response != null)
                    {
                        Console.WriteLine($"Зарегистрирована машина: Node ID: {response.garrageNodeId}, Vehicle ID: {response.VehicleId}");
                        vehicleNodeMapping.Add(response.VehicleId, "в пути");
                        Console.WriteLine($"Машина {response.VehicleId}, местоположение: {response.garrageNodeId} добавлена в словарь");
                        vehicleNodeMappingPlace.Add(response.VehicleId, response.serviceSpots);

                        availableVehicleId = response.VehicleId;
                        availableVehiclePlace = response.garrageNodeId;
                        availableVehiclePlacePlane = response.serviceSpots[request.AircraftCoordinates];

                        // Получаем маршрут
                        //Дефолт для проверки string[] routePoints = ["node1", "node2", "node3"];
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
                                    // Запрашиваем разрешение на передвижение 
                                    // Дефолт double distanse = 100;
                                    // Реальный запрос
                                    var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);

                                    if (distanse != null)
                                    {
                                        Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                        // Считаем время в пути
                                        int time = (int)Math.Ceiling((double)distanse / SpeedCar);
                                        await Task.Delay(time * 1000);
                                        // Уведомляем о прибытии
                                        await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                    }
                                }

                            string firstCar = "";

                            lock (lockObject)
                            {
                                if (startLoad == false)
                                {
                                    firstCar = availableVehicleId;
                                    startLoad = true;
                                }
                            }
                            if (firstCar == availableVehicleId)
                            {
                                // Сообщаем оркестратору о начале загрузки первой машиной
                                Console.WriteLine($"Сообщаем оркестратору о начале загрузки первой машиной {availableVehicleId}");
                                await informAboutStartLoading(request.AircraftId);
                            }

                            // Выполняем загрузку
                            Console.WriteLine($"Загрузка выполняется машиной {availableVehicleId}");
                            await Task.Delay(5000);

                            string lastCar = "";

                            lock (lockObject)
                            {
                                if (countCarsFinish == countCars)
                                {
                                    lastCar = availableVehicleId;
                                }
                                else countCarsFinish++;
                            }

                            if (lastCar == availableVehicleId)
                            {
                                // Сообщаем оркестратору об окончании загрузки последней машиной
                                Console.WriteLine($"Сообщаем оркестратору об окончании загрузки последней машиной {availableVehicleId}");
                                await informAboutFinishLoading(request.AircraftId, request.BaggageWeight);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Не удалось получить маршрут.");
                        }

                        // Получаем маршрут, чтобы поехать к гаражу
                        routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                        if (routePoints != null)
                        {
                            Console.WriteLine($"Маршрут для {availableVehicleId}:");
                            foreach (var point in routePoints)
                            {
                                Console.WriteLine($"ID точки: {point}");
                            }

                            // Движение по маршруту
                            for (int j = 0; j < routePoints.Length - 1; j++)
                            {
                                // Запрашиваем разрешение на передвижение
                                // Дефолт double distanse = 100;
                                // Реальный запрос
                                var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                if (distanse != null)
                                {
                                    Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                    // Считаем время в пути
                                    int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                    await Task.Delay(time * 1000);
                                    // Уведомляем о прибытии
                                    await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
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
        int countCars = (int)Math.Ceiling((double)request.BaggageWeight / _vehicleCapacity.Capacity);

        bool startLoad = false;

        int countCarsFinish = 1;

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
                    // Ищем свободную машину
                    var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "в пути");

                    // Проверяем, что такая машина найдена
                    if (foundVehicle.Key != null)
                    {
                        availableVehicleId = foundVehicle.Key;
                        availableVehiclePlace = foundVehicle.Value;

                        var innerDictionary = vehicleNodeMappingPlace[foundVehicle.Key];
                        availableVehiclePlacePlane = innerDictionary[request.AircraftCoordinates];
                        vehicleNodeMapping[foundVehicle.Key] = "в пути";
                    }
                }
                if (availableVehicleId != null)
                {
                    Console.WriteLine($"Найдена доступная машина: ID = {availableVehicleId}, местоположение = {availableVehiclePlace}");
                    // Получаем маршрут
                    // Дефолт для проверки string[] routePoints = ["node1", "node2", "node3"];
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
                            // Запрашиваем разрешение на передвижение
                            // Дефолт double distanse = 100;
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);

                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                // Считаем время в пути
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                // Уведомляем о прибытии
                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                            }
                        }

                        string firstCar = "";

                        lock (lockObject)
                        {
                            if (startLoad == false)
                            {
                                firstCar = availableVehicleId;
                                startLoad = true;
                            }
                        }
                        if (firstCar == availableVehicleId)
                        {
                            // Сообщаем оркестратору о начале разгрузки первой машиной
                            Console.WriteLine($"Сообщаем оркестратору о начале разгрузки первой машиной {availableVehicleId}");
                            await informAboutStartUploading(request.AircraftId);
                        }

                        // Выполняем разгрузку
                        Console.WriteLine($"Разгрузка выполняется машиной {availableVehicleId}");
                        await Task.Delay(5000);

                        string lastCar = "";

                        lock (lockObject)
                        {
                            if (countCarsFinish == countCars)
                            {
                                lastCar = availableVehicleId;
                            }
                            else countCarsFinish++;
                        }

                        if (lastCar == availableVehicleId)
                        {
                            // Сообщаем оркестратору об окончании разгрузки последней машиной
                            Console.WriteLine($"Сообщаем оркестратору об окончании разгрузки последней машиной {availableVehicleId}");
                            await informAboutFinishUploading(request.AircraftId, request.BaggageWeight);
                        } 
                    }
                    else
                    {
                        Console.WriteLine("Не удалось получить маршрут.");
                    }

                    // Получаем маршрут, чтобы поехать к гаражу
                    routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                    if (routePoints != null)
                    {
                        Console.WriteLine($"Маршрут для {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID точки: {point}");
                        }

                        // Движение по маршруту
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // Запрашиваем разрешение на передвижение 
                            // Дефолт double distanse = 100;
                            // Реальный запрос
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);

                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                // Считаем время в пути
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                // Уведомляем о прибытии
                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
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
                    var response = await RegisterVehicleAsync("baggage");

                    if (response != null)
                    {
                        Console.WriteLine($"Зарегистрирована машина: Node ID: {response.garrageNodeId}, Vehicle ID: {response.VehicleId}");
                        vehicleNodeMapping.Add(response.VehicleId, "в пути");
                        Console.WriteLine($"Машина {response.VehicleId}, местоположение: {response.garrageNodeId} добавлена в словарь");
                        vehicleNodeMappingPlace.Add(response.VehicleId, response.serviceSpots);

                        availableVehicleId = response.VehicleId;
                        availableVehiclePlace = response.garrageNodeId;
                        availableVehiclePlacePlane = response.serviceSpots[request.AircraftCoordinates];

                        // Получаем маршрут
                        //Дефолт для проверки string[] routePoints = ["node1", "node2", "node3"];
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
                                // Запрашиваем разрешение на передвижение 
                                // Дефолт double distanse = 100;
                                // Реальный запрос
                                var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);

                                if (distanse != null)
                                {
                                    Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                    // Считаем время в пути
                                    int time = (int)Math.Ceiling((double)distanse / SpeedCar);
                                    await Task.Delay(time * 1000);
                                    // Уведомляем о прибытии
                                    await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                }
                            }

                            string firstCar = "";

                            lock (lockObject)
                            {
                                if (startLoad == false)
                                {
                                    firstCar = availableVehicleId;
                                    startLoad = true;
                                }
                            }
                            if (firstCar == availableVehicleId)
                            {
                                // Сообщаем оркестратору о начале разгрузки первой машиной
                                Console.WriteLine($"Сообщаем оркестратору о начале разгрузки первой машиной {availableVehicleId}");
                                await informAboutStartUploading(request.AircraftId);
                            }

                            // Выполняем разгрузку
                            Console.WriteLine($"Разгрузка выполняется машиной {availableVehicleId}");
                            await Task.Delay(5000);

                            string lastCar = "";

                            lock (lockObject)
                            {
                                if (countCarsFinish == countCars)
                                {
                                    lastCar = availableVehicleId;
                                }
                                else countCarsFinish++;
                            }

                            if (lastCar == availableVehicleId)
                            {
                                // Сообщаем оркестратору об окончании разгрузки последней машиной
                                Console.WriteLine($"Сообщаем оркестратору об окончании разгрузки последней машиной {availableVehicleId}");
                                await informAboutFinishUploading(request.AircraftId, request.BaggageWeight);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Не удалось получить маршрут.");
                        }

                        // Получаем маршрут, чтобы поехать к гаражу
                        routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                        if (routePoints != null)
                        {
                            Console.WriteLine($"Маршрут для {availableVehicleId}:");
                            foreach (var point in routePoints)
                            {
                                Console.WriteLine($"ID точки: {point}");
                            }

                            // Движение по маршруту
                            for (int j = 0; j < routePoints.Length - 1; j++)
                            {
                                // Запрашиваем разрешение на передвижение
                                // Дефолт double distanse = 100;
                                // Реальный запрос
                                var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                if (distanse != null)
                                {
                                    Console.WriteLine($"{availableVehicleId} двигается от {routePoints[j]} до {routePoints[j + 1]}");
                                    // Считаем время в пути
                                    int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                    await Task.Delay(time * 1000);
                                    // Уведомляем о прибытии
                                    await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
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

    [HttpGet("getCapacity")]
    public ActionResult<VehicleCapacity> GetVehicleCapacity()
    {
        Console.WriteLine($"Отдаю вместимость машины {_vehicleCapacity.Capacity}"); 
        return Ok(_vehicleCapacity);
    }

    [HttpPost("updateCapacity")]
    public ActionResult SetVehicleCapacity([FromBody] VehicleCapacity newCapacity)
    {
        Console.WriteLine("Обновляю вместимость машины");
        if (newCapacity == null || newCapacity.Capacity < 3000)
        {
            return BadRequest("Некорректное значение. Необходимо ввести число больше 3000");
        }

        _vehicleCapacity = newCapacity;
        return Ok(_vehicleCapacity);
    }
}
