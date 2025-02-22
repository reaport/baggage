using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class BaggageController : ControllerBase
{
    public class BaggageLoadRequest
    {
        public string AircraftId { get; set; }
        public float BaggageWeight { get; set; }
        public int AircraftCoordinates { get; set; }
    }

    public class BaggageUploadRequest
    {
        public string AircraftId { get; set; }
        public float BaggageWeight { get; set; }
        public int AircraftCoordinates { get; set; }
    }

    public class BaggageResponse
    {
        public bool Waiting { get; set; }
    }

    public class ErrorResponse
    {
        public string Error { get; set; }
    }

    [HttpPost("load")]
    public ActionResult<BaggageResponse> LoadBaggage([FromBody] BaggageLoadRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0)
        {
            return BadRequest(new ErrorResponse { Error = "�������� ������" });
        }

        // ����� ������ ���� ������ ��������� ��������


        // ���� ��������� ����� ���
        bool waiting = false; // �������� �� ������
        return Ok(new BaggageResponse { Waiting = waiting });
    }

    [HttpPost("upload")]
    public ActionResult<BaggageResponse> UploadBaggage([FromBody] BaggageUploadRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.BaggageWeight <= 0)
        {
            return BadRequest(new ErrorResponse { Error = "�������� ������" });
        }

        // ����� ������ ���� ������ ��������� ��������


        // ���� ��������� ����� ���
        bool waiting = false; // ��������
        return Ok(new BaggageResponse { Waiting = waiting });
    }
}
