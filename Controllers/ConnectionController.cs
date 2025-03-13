using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Data;
using StudentTracker.Models;
using System.Security.Claims;

namespace StudentTracker.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ConnectionController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestConnection(ConnectionRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirst(ClaimTypes.Role);
            
            if (userIdClaim == null || userTypeClaim == null)
                return Unauthorized("User not authenticated");

            var currentUserId = int.Parse(userIdClaim.Value);
            
            if (userTypeClaim.Value != "Parent")
                return BadRequest("Only parents can send connection requests");

            var parent = await _context.Parents.FindAsync(currentUserId);
            if (parent == null)
                return NotFound("Parent not found");

            var student = await _context.Students.FirstOrDefaultAsync(s => s.Username == request.StudentUsername);
            if (student == null)
                return NotFound("Student not found");

            var existingConnection = await _context.StudentParentConnections
                .FirstOrDefaultAsync(c => c.StudentId == student.StudentId && c.ParentId == currentUserId);

            if (existingConnection != null)
                return BadRequest("Connection request already exists");

            var connection = new StudentParentConnection
            {
                StudentId = student.StudentId,
                ParentId = currentUserId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.StudentParentConnections.Add(connection);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Connection request sent successfully" });
        }

        [HttpPost("{connectionId}/accept")]
        public async Task<IActionResult> AcceptConnection(int connectionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirst(ClaimTypes.Role);
            
            if (userIdClaim == null || userTypeClaim == null)
                return Unauthorized("User not authenticated");

            var currentUserId = int.Parse(userIdClaim.Value);
            
            if (userTypeClaim.Value != "Student")
                return BadRequest("Only students can accept connection requests");

            var connection = await _context.StudentParentConnections
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId && c.StudentId == currentUserId);

            if (connection == null)
                return NotFound("Connection request not found");

            if (connection.Status != "Pending")
                return BadRequest("Connection request already processed");

            connection.Status = "Approved";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Connection request approved" });
        }

        [HttpPost("{connectionId}/reject")]
        public async Task<IActionResult> RejectConnection(int connectionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirst(ClaimTypes.Role);
            
            if (userIdClaim == null || userTypeClaim == null)
                return Unauthorized("User not authenticated");

            var currentUserId = int.Parse(userIdClaim.Value);
            
            if (userTypeClaim.Value != "Student")
                return BadRequest("Only students can reject connection requests");

            var connection = await _context.StudentParentConnections
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId && c.StudentId == currentUserId);

            if (connection == null)
                return NotFound("Connection request not found");

            if (connection.Status != "Pending")
                return BadRequest("Connection request already processed");

            connection.Status = "Rejected";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Connection request rejected" });
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingConnections()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirst(ClaimTypes.Role);
            
            if (userIdClaim == null || userTypeClaim == null)
                return Unauthorized("User not authenticated");

            var currentUserId = int.Parse(userIdClaim.Value);

            if (userTypeClaim.Value == "Student")
            {
                var studentConnections = await _context.StudentParentConnections
                    .Include(c => c.Parent)
                    .Where(c => c.StudentId == currentUserId && c.Status == "Pending")
                    .Select(c => new
                    {
                        c.ConnectionId,
                        c.Status,
                        c.CreatedAt,
                        Parent = c.Parent == null ? null : new
                        {
                            c.Parent.ParentId,
                            c.Parent.Username,
                            c.Parent.Email,
                            c.Parent.Fullname
                        }
                    })
                    .ToListAsync();

                return Ok(studentConnections);
            }
            else if (userTypeClaim.Value == "Parent")
            {
                var parentConnections = await _context.StudentParentConnections
                    .Include(c => c.Student)
                    .Where(c => c.ParentId == currentUserId && c.Status == "Pending")
                    .Select(c => new
                    {
                        c.ConnectionId,
                        c.Status,
                        c.CreatedAt,
                        Student = c.Student == null ? null : new
                        {
                            c.Student.StudentId,
                            c.Student.Username,
                            c.Student.Email,
                            c.Student.Fullname
                        }
                    })
                    .ToListAsync();

                return Ok(parentConnections);
            }

            return BadRequest("Invalid user type");
        }

        [HttpGet("connected")]
        public async Task<IActionResult> GetConnectedUsers()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var userTypeClaim = User.FindFirst(ClaimTypes.Role);
            
            if (userIdClaim == null || userTypeClaim == null)
                return Unauthorized("User not authenticated");

            var currentUserId = int.Parse(userIdClaim.Value);

            if (userTypeClaim.Value == "Student")
            {
                var connectedParents = await _context.StudentParentConnections
                    .Include(c => c.Parent)
                    .Where(c => c.StudentId == currentUserId && c.Status == "Approved")
                    .Select(c => new
                    {
                        c.Parent.ParentId,
                        c.Parent.Username,
                        c.Parent.Email,
                        c.Parent.Fullname
                    })
                    .ToListAsync();

                return Ok(connectedParents);
            }
            else if (userTypeClaim.Value == "Parent")
            {
                var connectedStudents = await _context.StudentParentConnections
                    .Include(c => c.Student)
                    .Where(c => c.ParentId == currentUserId && c.Status == "Approved")
                    .Select(c => new
                    {
                        c.Student.StudentId,
                        c.Student.Username,
                        c.Student.Email,
                        c.Student.Fullname
                    })
                    .ToListAsync();

                return Ok(connectedStudents);
            }

            return BadRequest("Invalid user type");
        }
    }
} 