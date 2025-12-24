using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SSSP.BL.DTOs.Tracking;
using SSSP.BL.Managers.Interfaces;


namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/tracking")]
    public class TrackingController : ControllerBase
    {
        private readonly IFaceTrackingManager _tracker;


        public TrackingController(IFaceTrackingManager tracker)
        {
            _tracker = tracker;
        }


        [HttpGet("active")]
        public ActionResult<IEnumerable<UserTrackingSession>> GetActive()
        {
            return Ok(_tracker.GetActiveSessions());
        }
    }
}