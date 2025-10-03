//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using Microsoft.AspNetCore.Mvc;
using PosKernel.HttpService.Models;
using PosKernel.HttpService.Services;

namespace PosKernel.HttpService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionStorage _sessionStorage;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(ISessionStorage sessionStorage, ILogger<SessionsController> logger)
    {
        _sessionStorage = sessionStorage;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<SessionResponse>> CreateSession([FromBody] SessionRequest request)
    {
        _logger.LogInformation("HTTP POST /api/sessions");
        var session = await _sessionStorage.CreateSessionAsync(request);
        return CreatedAtAction(nameof(GetSession), new { sessionId = session.SessionId }, session);
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<SessionResponse>> GetSession(string sessionId)
    {
        _logger.LogInformation("HTTP GET /api/sessions/{SessionId}", sessionId);
        var session = await _sessionStorage.GetSessionAsync(sessionId);
        if (session == null)
        {
            return NotFound();
        }
        return Ok(session);
    }

    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        _logger.LogInformation("HTTP DELETE /api/sessions/{SessionId}", sessionId);
        var deleted = await _sessionStorage.DeleteSessionAsync(sessionId);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }
}
