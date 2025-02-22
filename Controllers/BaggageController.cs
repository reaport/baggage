using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

[ApiController]
public class BaggageController : ControllerBase
{
    private static readonly HttpClient client = new();
    public class BaggageLoadRequest
    {
        public string AircraftId { get; set; }
        public float BaggageWeight { get; set; }
        public string AircraftCoordinates { get; set; }
    }

    public class BaggageUploadRequest
    {
        public string AircraftId { get; set; }
        public float BaggageWeight { get; set; }
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

    [HttpPost("load")]
    public async Task<ActionResult<BaggageResponse>> LoadBaggage([FromBody] BaggageLoadRequest request)
    {
        // Проверка на корректный запрос
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            return BadRequest(new ErrorResponse { Error = "Неверный запрос" });
        }

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









        // Здесь должна быть логика обработки загрузки


        // Если свободных машин нет
        bool waiting = false; // Заменить на логику
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
