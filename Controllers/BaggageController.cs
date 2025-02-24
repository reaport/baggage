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
    private double VehicleCapacity = 500.0;
    
    // �������� ������ (� �/�)
    private const double SpeedCar = 8;

    // ������� ��� �������� �����
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

    public class MoveResponse
    {
        public double Distance { get; set; }
    }

    private async Task<double?> GetPermissionAsync(string vehicleId, string from, string to)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // ������� ���� �������
        var jsonData = new
        {
            vehicleId,
            vehicleType = "baggage",
            from,
            to
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // ���������� POST-������
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
            // ��������� ������ 400 (�������� ������)
            Console.WriteLine($"������ 400: �������� ������.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // ��������� ������ 403 (���������)
            Console.WriteLine("������ 403: ����������� ���������. � ��� ��� ���� ��� ���������� ����� ��������.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            //��������� ������ 404 
            Console.WriteLine("���� �� ����� �� ������.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // ��������� ������ 409 (���� �����)
            Console.WriteLine($"���� ������ �����, ���������� �����");
            // ��������� 1-��������� �������� ����� ��������� ������� �������
            await Task.Delay(1000);
            // ��������� ����� ������� � ���� �� �����������
            return await GetPermissionAsync(vehicleId, from, to);
        }
        else
        {
            // ��������� ������ ������
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"��������� ����������� ������: {response.ReasonPhrase}, ����������: {errorContent}");
        }

        return null; // ���� ������ �� ������, ���������� null
    }

    private static readonly object lockObject = new object(); // ������ ��� ����������
    private async void informAboutArrivalAsync(string vehicleId, string nodeId)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // ������� ���� �������
        var jsonData = new
        {
            vehicleId,
            vehicleType = "baggage",
            nodeId
        };
        var json = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await client.PostAsync("/arrived", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("����������� ������� ����������");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // ��������� ������ 400 (�������� ������)
            Console.WriteLine($"������ 400: �������� ������.");
        }
    }

    [HttpPost("load")]
    public async Task<ActionResult<BaggageResponse>> LoadBaggage([FromBody] BaggageLoadRequest request)
    {
        // �������� �� ���������� ������
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            return BadRequest(new ErrorResponse { Error = "�������� ������" });
        }

        // ���� ��������� ������ ����
        bool waiting = false;

        // ������� ���������� ����������� ��� ��������� �����
        int countCars = (int)Math.Ceiling((double)request.BaggageWeight / VehicleCapacity);

        var tasks = new List<Task>();

        for (int i = 0; i < countCars; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                Console.WriteLine($"���������� ������");

                string availableVehicleId = null;
                string availableVehiclePlace = null;

                // ����� �� ����� ���� � �� �� ������
                lock (lockObject)
                {

                    // ���� ��������� ������
                    var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "� ����");

                    // ���������, ��� ����� ������ �������
                    if (foundVehicle.Key != null)
                    {
                        availableVehicleId = foundVehicle.Key;
                        availableVehiclePlace = foundVehicle.Value;
                        vehicleNodeMapping[foundVehicle.Key] = "� ����";
                    }
                }
                if (availableVehicleId != null)
                {
                    Console.WriteLine($"������� ��������� ������: ID = {availableVehicleId}, �������������� = {availableVehiclePlace}");

                    bool back = false;

                    for (int k = 0; k < 2; k++)
                    {
                        // �������� �������
                        //������ ��� ��������
                        string[] routePoints = ["node1", "node2", "node3"];
                        // �������� ������
                        //var routePoints = await GetRouteAsync(availableVehiclePlace, request.AircraftCoordinates);

                        if (routePoints != null)
                        {
                            // ��������� ���������� ������
                            Console.WriteLine("�������:");
                            foreach (var point in routePoints)
                            {
                                Console.WriteLine($"ID �����: {point}");
                            }

                            // �������� �� ��������
                            for (int j = 0; j < routePoints.Length - 1; j++)
                            {
                                // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                                // ������
                                double distanse = 100;
                                // �������� ������
                                //var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                // ��� ����?
                                if (distanse != null)
                                {
                                    // ������� ����� � ����
                                    int time = (int)Math.Ceiling(distanse / SpeedCar);
                                    await Task.Delay(time * 1000);

                                    // ���������� � ��������
                                    // �������� ������
                                    //informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                }
                            }

                            //��������, ��� ������� �� ��������� ����� ������������
                            // ���� ��������� � ������� - ��������� ��������
                            if (back == false)
                            {
                                // ��������� ��������
                                Console.WriteLine("�������� �����������");
                                await Task.Delay(5000);
                                back = true;
                            }
                            // ���� ��������� � ������, �������� ������ ��� ���������
                            else
                            {
                                Console.WriteLine($"������ {availableVehicleId} ������� � �����");
                                vehicleNodeMapping[availableVehicleId] = availableVehiclePlace;
                            }

                        }
                        else
                        {
                            Console.WriteLine("�� ������� �������� �������.");
                        }
                    }
                }
                else
                {
                    waiting = false;
                    Console.WriteLine("�����������");
                    // ����������� ����� ������
                    // �������� ������
                    /*var response = await RegisterVehicleAsync("baggage");

                    if (response != null)
                    {
                        Console.WriteLine($"���������������� ������: Node ID: {response.NodeId}, Vehicle ID: {response.VehicleId}");
                        vehicleNodeMapping.Add(response.VehicleId, response.NodeId);
                        i--;
                    }
                    else
                    {
                        Console.WriteLine("������ ���������� ���������, ������ �����������.");
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
            return BadRequest(new ErrorResponse { Error = "�������� ������" });
        }

        // ����� ������ ���� ������ ��������� ��������


        // ���� ��������� ����� ���
        bool waiting = false; // ��������
        return Ok(new BaggageResponse { Waiting = waiting });
    }
}
