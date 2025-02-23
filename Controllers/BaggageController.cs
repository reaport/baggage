using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

[ApiController]
public class BaggageController : ControllerBase
{
    // ����������� �� ���������� �����

    private static readonly HttpClient client = new();

    // ����������� ����� ������
    private const double VehicleCapacity = 500.0;

    // ������� ��� �������� �����
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

    private async Task<string[]?> GetRouteAsync(string from, string to)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // ������� ���� �������
        var jsonData = new
        {
            from,
            to,
            type = "baggage"
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await client.PostAsync("/route", content);

        if (response.IsSuccessStatusCode)
        {
            // ������ � �������������� ������
            var result = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<string[]>(result);
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Console.WriteLine("������� �� ������.");
        }
        else
        {
            // ��������� ������ ������
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"��������� ������: {response.ReasonPhrase}, ����������: {errorContent}");
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

        // ������� ���������� ����������� ��� ��������� �����
        int countCars = (int)Math.Ceiling((double)request.BaggageWeight / VehicleCapacity);

        for (int i = 0; i < countCars; i++)
        {
            // ���� ��������� ������
            var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "� ����");

            // ���������, ��� ����� ������ �������
            if (foundVehicle.Key != null)
            {
                string availableVehicleId = foundVehicle.Key;
                string availableVehiclePlace = foundVehicle.Value;

                Console.WriteLine($"������� ��������� ������: ID = {availableVehicleId}, �������������� = {availableVehiclePlace}");

                //������ ��� ��������
                string[] routePoints = ["node1", "node2", "node3"];

                // �������� �������
                // �������� ������
                //var routePoints = await GetRouteAsync(availableVehiclePlace, request.AircraftCoordinates);
                if (routePoints != null)
                {
                    // ��������� ���������� ������
                    foreach (var point in routePoints)
                    {
                        Console.WriteLine("�������:");
                        Console.WriteLine($"ID �����: {point}");
                        // � �����:
                        // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                        // ������� ����� ������������
                        // ���������� � ��������

                        //��������, ��� ������� �� ��������� �����
                        //����������� � �����
                    }

                }
                else
                {
                    Console.WriteLine("�� ������� �������� �������.");
                }
            }
            else
            {
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
            }











            // ����� ������ ���� ������ ��������� ��������


            // ���� ��������� ����� ���
            bool waiting = false; // �������� �� ������
            return Ok(new BaggageResponse { Waiting = waiting });
        }
        return Ok(new BaggageResponse { Waiting = true });
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
