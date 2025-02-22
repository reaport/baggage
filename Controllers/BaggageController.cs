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
        // ������������ URL ��� �������
        string url = $"/register-vehicle/{vehicleType}";

        // �������� POST-�������
        var response = await client.PostAsync(url, null);

        if (response.IsSuccessStatusCode)
        {
            // ������ ������, ���� ������ ������� (��� 200)
            var result = await response.Content.ReadFromJsonAsync<RegisterVehicleResponse>();
            return result; // ���������� ���������
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // ��������� ������ Bad Request (��� 400)
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error 400: �������� ������ �������. {error}");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // ��������� ������ Forbidden (��� 403)
            Console.WriteLine("Error 403: ��� ���������� ����.");
        }
        else
        {
            // ��������� ������ ������
            Console.WriteLine($"��������� ������: {response.StatusCode}");
        }

        return null; // ���� ������ �� ������, ���������� null
    }

    [HttpPost("load")]
    public async Task<ActionResult<BaggageResponse>> LoadBaggage([FromBody] BaggageLoadRequest request)
    {
        // �������� �� ���������� ������
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            return BadRequest(new ErrorResponse { Error = "�������� ������" });
        }

        // ����������� ����� ������
        string vehicleType = "baggage"; 
        var response = await RegisterVehicleAsync(vehicleType);

        if (response != null)
        {
            Console.WriteLine($"Node ID: {response.NodeId}, Vehicle ID: {response.VehicleId}");
            // ����� ������
        }
        else
        {
            Console.WriteLine("������ ���������� ���������, ������ �����������.");
        }









        // ����� ������ ���� ������ ��������� ��������


        // ���� ��������� ����� ���
        bool waiting = false; // �������� �� ������
        return Ok(new BaggageResponse { Waiting = waiting });
    }

    [HttpPost("upload")]
    public ActionResult<BaggageResponse> UploadBaggage([FromBody] BaggageUploadRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            return BadRequest(new ErrorResponse { Error = "�������� ������" });
        }

        // ����� ������ ���� ������ ��������� ��������


        // ���� ��������� ����� ���
        bool waiting = false; // ��������
        return Ok(new BaggageResponse { Waiting = waiting });
    }
}
