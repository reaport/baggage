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
    // ����������� ����� ������
    private static VehicleCapacity _vehicleCapacity = new VehicleCapacity { Capacity = 500 };

    //private static double VehicleCapacity = 500.0;

    // �������� ������ (� �/�)
    private const double SpeedCar = 8;

    // ������� ��� �������� �����
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
        // ������������ URL ��� �������
        string url = $"https://ground-control.reaport.ru/register-vehicle/{vehicleType}";

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
        HttpClient client = new();
        Console.WriteLine("����� � �������");
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

        Console.WriteLine("������� ��������� ���� ������");
        // ���������� POST-������
        var response = await client.PostAsync("https://ground-control.reaport.ru/route", content);
        Console.WriteLine("���� ������ ���������");

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
        HttpClient client = new();
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
            // ��������� ������ 400 (�������� ������)
            Console.WriteLine($"{vehicleId}: ������ 400 GetPermissionAsync: �������� ������. ������ ������ ���������� ������� �� ���� {from} � ���� {to}");
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
            Console.WriteLine($"{vehicleId}: ������� ������� �� ���� {from}. ���� {to} ������ �����, ���������� �����");
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

    private async Task informAboutArrivalAsync(string vehicleId, string nodeId)
    {
        HttpClient client = new();
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
        var response = await client.PostAsync("https://ground-control.reaport.ru/arrived", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"����������� � �������� ������� ����������. ������ {vehicleId} ������� � ���� {nodeId}"); 
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // ��������� ������ 400 (�������� ������)
            Console.WriteLine($"������ 400 informAboutArrivalAsync: �������� ������. ������ {vehicleId} �� ������� � ���� {nodeId}");
        }
    }

    private static readonly object lockObject = new object(); // ������ ��� ����������

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
        int countCars = (int)Math.Ceiling((double)request.BaggageWeight / _vehicleCapacity.Capacity);

        // ���������, ���� �� ���� �� ���� ��������� ������
        lock (lockObject)
        {
            var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "� ����");
            if (foundVehicle.Key == null)
            {
                waiting = true; // ���� ��� ��������� �����
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

                // ����� �� ����� ���� � �� �� ������
                lock (lockObject)
                {
                    foreach (var innerPair in vehicleNodeMapping)
                    {
                        Console.WriteLine($"  0���� ����������� �������: {innerPair.Key}, ��������: {innerPair.Value}");
                    }
                    // ���� ��������� ������
                    var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "� ����");

                    // ���������, ��� ����� ������ �������
                    if (foundVehicle.Key != null)
                    {
                        /*foreach (var innerPair in vehicleNodeMapping)
                        {
                            if (innerPair.Value != "� ����")
                            {
                                availableVehicleId = innerPair.Key;
                                availableVehiclePlace = innerPair.Value;
                            }
                        }*/
                        /*foreach (var outerPair in vehicleNodeMappingPlace)
                        {
                            Console.WriteLine($"���� �������� �������: {outerPair.Key}");
                            foreach (var innerPair in outerPair.Value)
                            {
                                Console.WriteLine($"  ���� ����������� �������: {innerPair.Key}, ��������: {innerPair.Value}");
                            }
                        }*/
                        availableVehicleId = foundVehicle.Key;
                        availableVehiclePlace = foundVehicle.Value;
                        //Console.WriteLine($"������ {response.VehicleId}, �������������� ������� �������: {response.serviceSpots[request.AircraftCoordinates]} ��������� � �������");
                        
                        var innerDictionary = vehicleNodeMappingPlace[foundVehicle.Key];
                        availableVehiclePlacePlane = innerDictionary[request.AircraftCoordinates];
                        vehicleNodeMapping[foundVehicle.Key] = "� ����";
                        foreach (var innerPair in vehicleNodeMapping)
                        {
                            Console.WriteLine($"  1���� ����������� �������: {innerPair.Key}, ��������: {innerPair.Value}");
                        }
                    }
                }
                if (availableVehicleId != null)
                {
                    foreach (var innerPair in vehicleNodeMapping)
                    {
                        Console.WriteLine($"2  ���� ����������� �������: {innerPair.Key}, ��������: {innerPair.Value}");
                    }
                    Console.WriteLine($"������� ��������� ������: ID = {availableVehicleId}, �������������� = {availableVehiclePlace}");
                    // �������� �������
                    //������ ��� ��������
                    //string[] routePoints = ["node1", "node2", "node3"];
                    // �������� ������
                    var routePoints = await GetRouteAsync(availableVehiclePlace, availableVehiclePlacePlane);

                    if (routePoints != null)
                    {
                        // ��������� ���������� ������
                        Console.WriteLine($"������� ��� {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID �����: {point}");
                        }

                        // �������� �� ��������
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                            // ������
                            //double distanse = 100;
                            // �������� ������
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                            // ��� ����?
                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} ��������� �� {routePoints[j]} �� {routePoints[j + 1]}");
                                // ������� ����� � ����
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                // ���������� � ��������
                                // �������� ������

                            }
                        }

                        //��������, ��� ������� �� ��������� ����� ������������
                        // ���� ��������� � ������� - ��������� ��������

                        // ��������� ��������
                        Console.WriteLine("�������� �����������");
                        await Task.Delay(5000);
                    }
                    else
                    {
                        Console.WriteLine("�� ������� �������� �������.");
                    }

                    // �������� �������, ����� ������� � ������
                    routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                    if (routePoints != null)
                    {
                        // ��������� ���������� ������
                        Console.WriteLine($"������� ��� {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID �����: {point}");
                        }

                        // �������� �� ��������
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                            // ������
                            //double distanse = 100;
                            // �������� ������
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                            // ��� ����?
                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} ��������� �� {routePoints[j]} �� {routePoints[j + 1]}");
                                // ������� ����� � ����
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                // ���������� � ��������
                                // �������� ������
                            }
                        }
                        // �������� ������ ��� ���������

                        Console.WriteLine($"������ {availableVehicleId} ������� � �����");
                        vehicleNodeMapping[availableVehicleId] = availableVehiclePlace;
                    }
                    else
                    {
                        Console.WriteLine("�� ������� �������� �������.");
                    }
                }
                else
                {
                    // ����������� ����� ������ � ����������� � � �������
                    // �������� ������
                    Console.WriteLine("������� ����� � ������� ���������������� ������");
                    var response = await RegisterVehicleAsync("baggage");
                    Console.WriteLine("����� �� ������� �����������");

                    if (response != null)
                    {
                        Console.WriteLine($"���������������� ������: Node ID: {response.garrageNodeId}, Vehicle ID: {response.VehicleId}");
                        vehicleNodeMapping.Add(response.VehicleId, "� ����");
                        Console.WriteLine($"������ {response.VehicleId}, ��������������: {response.garrageNodeId} ��������� � �������");
                        vehicleNodeMappingPlace.Add(response.VehicleId, response.serviceSpots);
                        Console.WriteLine($"������ {response.VehicleId}, �������������� ������� �������: {response.serviceSpots[request.AircraftCoordinates]} ��������� � �������");

                        availableVehicleId = response.VehicleId;
                        availableVehiclePlace = response.garrageNodeId;
                        //var innerDictionary = vehicleNodeMappingPlace[response.garrageNodeId];
                        availableVehiclePlacePlane = response.serviceSpots[request.AircraftCoordinates];
                        Console.WriteLine($"�������� � �������: {availableVehiclePlacePlane}");

                        // �������� �������
                        //������ ��� ��������
                        //string[] routePoints = ["node1", "node2", "node3"];
                        // �������� ������ 
                        Console.WriteLine($"������� ����� � ������� �������� ������� �� {availableVehiclePlace} �� {availableVehiclePlacePlane}");
                        var routePoints = await GetRouteAsync(availableVehiclePlace, availableVehiclePlacePlane);

                            if (routePoints != null)
                            {
                                // ��������� ���������� ������
                                Console.WriteLine($"������� ��� {availableVehicleId}:");
                                foreach (var point in routePoints)
                                {
                                    Console.WriteLine($"ID �����: {point}");
                                }

                                // �������� �� ��������
                                for (int j = 0; j < routePoints.Length - 1; j++)
                                {
                                    // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                                    // ������
                                    //double distanse = 100;
                                    // �������� ������
                                    var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                    // ��� ����?
                                    if (distanse != null)
                                    {
                                        Console.WriteLine($"{availableVehicleId} ��������� �� {routePoints[j]} �� {routePoints[j + 1]}");
                                        // ������� ����� � ����
                                        int time = (int)Math.Ceiling((double)distanse / SpeedCar);
                                    // ��������
                                        await Task.Delay(time * 100);

                                        await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                        // ���������� � ��������
                                        // �������� ������

                                    }
                                }

                                //��������, ��� ������� �� ��������� ����� ������������
                                // ���� ��������� � ������� - ��������� ��������

                                // ��������� ��������
                                Console.WriteLine("�������� �����������");
                                await Task.Delay(500);
                            }
                            else
                            {
                                Console.WriteLine("�� ������� �������� �������.");
                            }

                            // �������� �������, ����� ������� � ������
                            routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                            if (routePoints != null)
                            {
                                // ��������� ���������� ������
                                Console.WriteLine($"������� ��� {availableVehicleId}:");
                                foreach (var point in routePoints)
                                {
                                    Console.WriteLine($"ID �����: {point}");
                                }

                                // �������� �� ��������
                                for (int j = 0; j < routePoints.Length - 1; j++)
                                {
                                    // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                                    // ������
                                    //double distanse = 100;
                                    // �������� ������
                                    var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                    // ��� ����?
                                    if (distanse != null)
                                    {
                                        Console.WriteLine($"{availableVehicleId} ��������� �� {routePoints[j]} �� {routePoints[j + 1]}");
                                        // ������� ����� � ����
                                        int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                        await Task.Delay(time * 100);

                                        await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                        // ���������� � ��������
                                        // �������� ������
                                    }
                                }
                                // �������� ������ ��� ���������

                                Console.WriteLine($"������ {availableVehicleId} ������� � �����");
                                vehicleNodeMapping[availableVehicleId] = availableVehiclePlace;
                            }
                            else
                            {
                                Console.WriteLine("�� ������� �������� �������.");
                            }
                        
                    }
                    else
                    {
                        Console.WriteLine("������ ���������� ���������, ������ �����������.");
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
        // �������� �� ���������� ������
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0 || string.IsNullOrEmpty(request.AircraftCoordinates))
        {
            return BadRequest(new ErrorResponse { Error = "�������� ������" });
        }

        // ���� ��������� ������ ����
        bool waiting = false;

        // ������� ���������� ����������� ��� ��������� �����
        int countCars = (int)Math.Ceiling((double)request.BaggageWeight / _vehicleCapacity.Capacity);

        // ���������, ���� �� ���� �� ���� ��������� ������
        lock (lockObject)
        {
            var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "� ����");
            if (foundVehicle.Key == null)
            {
                waiting = true; // ���� ��� ��������� �����
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

                // ����� �� ����� ���� � �� �� ������
                lock (lockObject)
                {
                    foreach (var innerPair in vehicleNodeMapping)
                    {
                        Console.WriteLine($"  0���� ����������� �������: {innerPair.Key}, ��������: {innerPair.Value}");
                    }
                    // ���� ��������� ������
                    var foundVehicle = vehicleNodeMapping.FirstOrDefault(nodeId => nodeId.Value != "� ����");

                    // ���������, ��� ����� ������ �������
                    if (foundVehicle.Key != null)
                    {
                        /*foreach (var innerPair in vehicleNodeMapping)
                        {
                            if (innerPair.Value != "� ����")
                            {
                                availableVehicleId = innerPair.Key;
                                availableVehiclePlace = innerPair.Value;
                            }
                        }*/
                        /*foreach (var outerPair in vehicleNodeMappingPlace)
                        {
                            Console.WriteLine($"���� �������� �������: {outerPair.Key}");
                            foreach (var innerPair in outerPair.Value)
                            {
                                Console.WriteLine($"  ���� ����������� �������: {innerPair.Key}, ��������: {innerPair.Value}");
                            }
                        }*/
                        availableVehicleId = foundVehicle.Key;
                        availableVehiclePlace = foundVehicle.Value;
                        //Console.WriteLine($"������ {response.VehicleId}, �������������� ������� �������: {response.serviceSpots[request.AircraftCoordinates]} ��������� � �������");

                        var innerDictionary = vehicleNodeMappingPlace[foundVehicle.Key];
                        availableVehiclePlacePlane = innerDictionary[request.AircraftCoordinates];
                        vehicleNodeMapping[foundVehicle.Key] = "� ����";
                        foreach (var innerPair in vehicleNodeMapping)
                        {
                            Console.WriteLine($"  1���� ����������� �������: {innerPair.Key}, ��������: {innerPair.Value}");
                        }
                    }
                }
                if (availableVehicleId != null)
                {
                    foreach (var innerPair in vehicleNodeMapping)
                    {
                        Console.WriteLine($"2  ���� ����������� �������: {innerPair.Key}, ��������: {innerPair.Value}");
                    }
                    Console.WriteLine($"������� ��������� ������: ID = {availableVehicleId}, �������������� = {availableVehiclePlace}");
                    // �������� �������
                    //������ ��� ��������
                    //string[] routePoints = ["node1", "node2", "node3"];
                    // �������� ������
                    var routePoints = await GetRouteAsync(availableVehiclePlace, availableVehiclePlacePlane);

                    if (routePoints != null)
                    {
                        // ��������� ���������� ������
                        Console.WriteLine($"������� ��� {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID �����: {point}");
                        }

                        // �������� �� ��������
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                            // ������
                            //double distanse = 100;
                            // �������� ������
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                            // ��� ����?
                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} ��������� �� {routePoints[j]} �� {routePoints[j + 1]}");
                                // ������� ����� � ����
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                // ���������� � ��������
                                // �������� ������

                            }
                        }

                        //��������, ��� ������� �� ��������� ����� ������������
                        // ���� ��������� � ������� - ��������� ��������

                        // ��������� ��������
                        Console.WriteLine("�������� �����������");
                        await Task.Delay(5000);
                    }
                    else
                    {
                        Console.WriteLine("�� ������� �������� �������.");
                    }

                    // �������� �������, ����� ������� � ������
                    routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                    if (routePoints != null)
                    {
                        // ��������� ���������� ������
                        Console.WriteLine($"������� ��� {availableVehicleId}:");
                        foreach (var point in routePoints)
                        {
                            Console.WriteLine($"ID �����: {point}");
                        }

                        // �������� �� ��������
                        for (int j = 0; j < routePoints.Length - 1; j++)
                        {
                            // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                            // ������
                            //double distanse = 100;
                            // �������� ������
                            var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                            // ��� ����?
                            if (distanse != null)
                            {
                                Console.WriteLine($"{availableVehicleId} ��������� �� {routePoints[j]} �� {routePoints[j + 1]}");
                                // ������� ����� � ����
                                int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                await Task.Delay(time * 1000);

                                await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                // ���������� � ��������
                                // �������� ������
                            }
                        }
                        // �������� ������ ��� ���������

                        Console.WriteLine($"������ {availableVehicleId} ������� � �����");
                        vehicleNodeMapping[availableVehicleId] = availableVehiclePlace;
                    }
                    else
                    {
                        Console.WriteLine("�� ������� �������� �������.");
                    }
                }
                else
                {
                    // ����������� ����� ������ � ����������� � � �������
                    // �������� ������
                    Console.WriteLine("������� ����� � ������� ���������������� ������");
                    var response = await RegisterVehicleAsync("baggage");
                    Console.WriteLine("����� �� ������� �����������");

                    if (response != null)
                    {
                        Console.WriteLine($"���������������� ������: Node ID: {response.garrageNodeId}, Vehicle ID: {response.VehicleId}");
                        vehicleNodeMapping.Add(response.VehicleId, "� ����");
                        Console.WriteLine($"������ {response.VehicleId}, ��������������: {response.garrageNodeId} ��������� � �������");
                        vehicleNodeMappingPlace.Add(response.VehicleId, response.serviceSpots);
                        Console.WriteLine($"������ {response.VehicleId}, �������������� ������� �������: {response.serviceSpots[request.AircraftCoordinates]} ��������� � �������");

                        availableVehicleId = response.VehicleId;
                        availableVehiclePlace = response.garrageNodeId;
                        //var innerDictionary = vehicleNodeMappingPlace[response.garrageNodeId];
                        availableVehiclePlacePlane = response.serviceSpots[request.AircraftCoordinates];
                        Console.WriteLine($"�������� � �������: {availableVehiclePlacePlane}");

                        // �������� �������
                        //������ ��� ��������
                        //string[] routePoints = ["node1", "node2", "node3"];
                        // �������� ������ 
                        Console.WriteLine($"������� ����� � ������� �������� ������� �� {availableVehiclePlace} �� {availableVehiclePlacePlane}");
                        var routePoints = await GetRouteAsync(availableVehiclePlace, availableVehiclePlacePlane);

                        if (routePoints != null)
                        {
                            // ��������� ���������� ������
                            Console.WriteLine($"������� ��� {availableVehicleId}:");
                            foreach (var point in routePoints)
                            {
                                Console.WriteLine($"ID �����: {point}");
                            }

                            // �������� �� ��������
                            for (int j = 0; j < routePoints.Length - 1; j++)
                            {
                                // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                                // ������
                                //double distanse = 100;
                                // �������� ������
                                var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                // ��� ����?
                                if (distanse != null)
                                {
                                    Console.WriteLine($"{availableVehicleId} ��������� �� {routePoints[j]} �� {routePoints[j + 1]}");
                                    // ������� ����� � ����
                                    int time = (int)Math.Ceiling((double)distanse / SpeedCar);
                                    // ��������
                                    await Task.Delay(time * 100);

                                    await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                    // ���������� � ��������
                                    // �������� ������

                                }
                            }

                            //��������, ��� ������� �� ��������� ����� ������������
                            // ���� ��������� � ������� - ��������� ��������

                            // ��������� ��������
                            Console.WriteLine("�������� �����������");
                            await Task.Delay(500);
                        }
                        else
                        {
                            Console.WriteLine("�� ������� �������� �������.");
                        }

                        // �������� �������, ����� ������� � ������
                        routePoints = await GetRouteAsync(availableVehiclePlacePlane, availableVehiclePlace);

                        if (routePoints != null)
                        {
                            // ��������� ���������� ������
                            Console.WriteLine($"������� ��� {availableVehicleId}:");
                            foreach (var point in routePoints)
                            {
                                Console.WriteLine($"ID �����: {point}");
                            }

                            // �������� �� ��������
                            for (int j = 0; j < routePoints.Length - 1; j++)
                            {
                                // ����������� ���������� �� ������������ (��������, ���� ���������� ����)
                                // ������
                                //double distanse = 100;
                                // �������� ������
                                var distanse = await GetPermissionAsync(availableVehicleId, routePoints[j], routePoints[j + 1]);
                                // ��� ����?
                                if (distanse != null)
                                {
                                    Console.WriteLine($"{availableVehicleId} ��������� �� {routePoints[j]} �� {routePoints[j + 1]}");
                                    // ������� ����� � ����
                                    int time = (int)Math.Ceiling((double)distanse / SpeedCar);

                                    await Task.Delay(time * 100);

                                    await informAboutArrivalAsync(availableVehicleId, routePoints[j + 1]);
                                    // ���������� � ��������
                                    // �������� ������
                                }
                            }
                            // �������� ������ ��� ���������

                            Console.WriteLine($"������ {availableVehicleId} ������� � �����");
                            vehicleNodeMapping[availableVehicleId] = availableVehiclePlace;
                        }
                        else
                        {
                            Console.WriteLine("�� ������� �������� �������.");
                        }

                    }
                    else
                    {
                        Console.WriteLine("������ ���������� ���������, ������ �����������.");
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
        Console.WriteLine($"����� ����������� ������ {_vehicleCapacity.Capacity}"); 
        return Ok(_vehicleCapacity);
    }

    [HttpPost("updateCapacity")]
    public ActionResult SetVehicleCapacity([FromBody] VehicleCapacity newCapacity)
    {
        Console.WriteLine("�������� ����������� ������");
        if (newCapacity == null || newCapacity.Capacity < 0)
        {
            return BadRequest("Invalid capacity value.");
        }

        _vehicleCapacity = newCapacity;
        return Ok(_vehicleCapacity);
    }
}

// �������� ����� ������ (��� ������)
// �������������� � ������� (��� ������)
// ������� html ��������� 
// ��������� ���
// �������� ������������
