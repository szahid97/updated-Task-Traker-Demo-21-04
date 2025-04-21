using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using TaskTrackerDemo.Data;
using TaskTrackerDemo.Models;
using TaskTrackerDemo.Dtos;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TaskTrackerDemo.Controllers
{

    [Authorize]
    public class ProjectDetailsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectDetailsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // GET: ProjectDetails/5
        public async Task<IActionResult> ProjectDetails(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .Include(p => p.Discussions)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            await PopulateTeamMembersInViewBag();
            return View(project);
        }


         private async Task PopulateTeamMembersInViewBag()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var myTeamIds = await _context.MyTeams
                .Where(mt => mt.UserId == currentUser.Id)
                .Select(mt => mt.TeamMemberId)
                .ToListAsync();

            var teamMembers = await _context.Users
                .Where(u => myTeamIds.Contains(u.Id)|| u.Id == currentUser.Id) 
                .ToListAsync();

            ViewBag.TeamMembers = teamMembers;
        }


        [HttpPost]
        public async Task<IActionResult> EditTask(int taskId, string title, DateTime dueDate)
        {
            var task = await _context.ProjectTasks.FindAsync(taskId);
            if (task == null)
            {
                return NotFound();
            }

            var oldTitle = task.Title;
            var oldDueDate = task.DueDate;

            task.Title = title;
            task.DueDate = dueDate;
            _context.Update(task);

            // Add discussion entry
            var currentUser = await _userManager.GetUserAsync(User);
            var message = $"{currentUser.FullName} changed task '{oldTitle}' (Due: {oldDueDate:dd MMM yyyy}) to '{title}' (Due: {dueDate:dd MMM yyyy})";
            await AddDiscussionEntry(task.ProjectId, message);

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { id = task.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            var task = await _context.ProjectTasks.FindAsync(taskId);
            if (task == null)
            {
                return NotFound();
            }

            var projectId = task.ProjectId;
            var taskTitle = task.Title;

            _context.ProjectTasks.Remove(task);

            // Add discussion entry
            var currentUser = await _userManager.GetUserAsync(User);
            var message = $"{currentUser.FullName} deleted task '{taskTitle}'";
            await AddDiscussionEntry(projectId, message);

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { id = projectId });
        }

        [HttpPost]
        public async Task<IActionResult> ReassignTask(int taskId, string assignedToId)
        {
            var task = await _context.ProjectTasks.FindAsync(taskId);
            if (task == null)
            {
                return NotFound();
            }

            var oldAssignee = task.AssignedToId;
            var newAssignee = await _context.Users.FindAsync(assignedToId);

            task.AssignedToId = assignedToId;
            _context.Update(task);

            // Add discussion entry
            var currentUser = await _userManager.GetUserAsync(User);
            var message = $"{currentUser.FullName} reassigned task '{task.Title}' from {oldAssignee} to {newAssignee.FullName}";
            await AddDiscussionEntry(task.ProjectId, message);

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { id = task.ProjectId });
        }

        private async Task AddDiscussionEntry(int projectId, string message)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var discussion = new ProjectDiscussion
            {
                ProjectId = projectId,
                User = currentUser.FullName,
                Message = message,
                Timestamp = DateTime.Now
            };
            _context.ProjectDiscussions.Add(discussion);
        }
    }

}