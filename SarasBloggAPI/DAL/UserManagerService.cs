using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.DTOs;

namespace SarasBloggAPI.DAL
{
    public class UserManagerService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly MyDbContext _db;

        public UserManagerService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, MyDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        // SarasBloggAPI/DAL/UserManagerService.cs
        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var list = new List<UserDto>();
            foreach (var u in users)
            {
                list.Add(new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    Email = u.Email,
                    Name = u.Name,
                    BirthYear = u.BirthYear,
                    EmailConfirmed = u.EmailConfirmed,
                    Roles = (await _userManager.GetRolesAsync(u)).ToList(),
                    NotifyOnNewPost = u.NotifyOnNewPost,
                });
            }
            return list;
        }

        public async Task<UserDto?> GetUserByIdAsync(string id)
        {
            var u = await _userManager.FindByIdAsync(id);
            if (u is null) return null;
            var roles = await _userManager.GetRolesAsync(u);
            return new UserDto
            {
                Id = u.Id,
                UserName = u.UserName ?? "",
                Email = u.Email,
                Name = u.Name,
                BirthYear = u.BirthYear,
                EmailConfirmed = u.EmailConfirmed,
                Roles = roles.ToList(),
                NotifyOnNewPost = u.NotifyOnNewPost
            };
        }


        public async Task<BasicResultDto> ChangeUserNameAsync(string userId, string newUserName)
        {
            // enkel policy – justera vid behov
            if (!Regex.IsMatch(newUserName, "^[a-zA-Z0-9_.-]{3,30}$"))
                return new BasicResultDto { Succeeded = false, Message = "Invalid username format." };

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return new BasicResultDto { Succeeded = false, Message = "User not found." };

            if ((user.Email ?? "").Equals("admin@sarasblogg.se", StringComparison.OrdinalIgnoreCase))
                return new BasicResultDto { Succeeded = false, Message = "System user cannot be renamed." };

            var exists = await _userManager.FindByNameAsync(newUserName);
            if (exists is not null && exists.Id != user.Id)
                return new BasicResultDto { Succeeded = false, Message = "Username already taken." };

            var setRes = await _userManager.SetUserNameAsync(user, newUserName);
            if (!setRes.Succeeded)
                return new BasicResultDto
                {
                    Succeeded = false,
                    Message = string.Join("; ", setRes.Errors.Select(e => e.Description))
                };

            var upd = await _userManager.UpdateAsync(user);
            if (!upd.Succeeded)
                return new BasicResultDto
                {
                    Succeeded = false,
                    Message = string.Join("; ", upd.Errors.Select(e => e.Description))
                };

            return new BasicResultDto { Succeeded = true, Message = "Username updated." };
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded;
        }

        public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
        {
            return await _userManager.GetRolesAsync(user);
        }

        public async Task<bool> IsInRoleAsync(ApplicationUser user, string role)
        {
            return await _userManager.IsInRoleAsync(user, role);
        }

        public async Task<bool> AddToRoleAsync(ApplicationUser user, string role)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }

            var result = await _userManager.AddToRoleAsync(user, role);
            return result.Succeeded;
        }

        public async Task<bool> RemoveFromRoleAsync(ApplicationUser user, string role)
        {
            var result = await _userManager.RemoveFromRoleAsync(user, role);
            return result.Succeeded;
        }

        public async Task<IList<string>> GetUserRolesAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user == null ? new List<string>() : await _userManager.GetRolesAsync(user);
        }

        public async Task<bool> AddUserToRoleAsync(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            // OBS: admin@sarasblogg.se spärras INTE här – vi vill tillåta fler roller även om de inte visas som klickbara i frontend.
            var result = await _userManager.AddToRoleAsync(user, roleName);
            return result.Succeeded;
        }



        public async Task<bool> RemoveUserFromRoleAsync(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            if (user.Email?.ToLower() == "admin@sarasblogg.se")
                return false; // Förhindrar att ta bort roller från admin

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            return result.Succeeded;
        }

        public async Task<ApplicationUser?> FindUserEntityAsync(string id)
    => await _userManager.FindByIdAsync(id);

        public async Task<BasicResultDto> DeleteMyAccountAsync(string userId, string? password)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return new BasicResultDto { Succeeded = false, Message = "User not found." };

            // Har användaren lokalt lösen? då kräver vi korrekt lösen
            var hasPw = await _userManager.HasPasswordAsync(user);
            if (hasPw)
            {
                if (string.IsNullOrWhiteSpace(password))
                    return new BasicResultDto { Succeeded = false, Message = "Password is required." };

                var ok = await _userManager.CheckPasswordAsync(user, password);
                if (!ok) return new BasicResultDto { Succeeded = false, Message = "Invalid password." };
            }

            var del = await _userManager.DeleteAsync(user);
            if (!del.Succeeded)
                return new BasicResultDto
                {
                    Succeeded = false,
                    Message = string.Join("; ", del.Errors.Select(e => e.Description))
                };

            return new BasicResultDto { Succeeded = true, Message = "Account deleted." };
        }

        public async Task<(byte[] Bytes, string FileName, string ContentType)?> BuildPersonalDataFileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);

            var comments = await _db.Comments
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.BloggId, c.Content, c.CreatedAt })
                .ToListAsync();

            var likes = await _db.BloggLikes
                .Where(l => l.UserId == userId)
                .OrderBy(l => l.CreatedAt)
                .Select(l => new { l.Id, l.BloggId, l.CreatedAt })
                .ToListAsync();

            var bloggIds = comments.Select(c => c.BloggId).Concat(likes.Select(l => l.BloggId)).Distinct().ToList();
            var bloggTitles = await _db.Bloggs
                .Where(b => bloggIds.Contains(b.Id))
                .Select(b => new { b.Id, b.Title })
                .ToDictionaryAsync(x => x.Id, x => x.Title ?? "");

            var export = new
            {
                User = new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.Name,
                    user.BirthYear,
                    user.EmailConfirmed,
                    Roles = roles
                },
                Comments = comments.Select(c => new
                {
                    c.Id,
                    c.BloggId,
                    BloggTitle = bloggTitles.TryGetValue(c.BloggId, out var t1) ? t1 : "",
                    c.Content,
                    c.CreatedAt
                }),
                Likes = likes.Select(l => new
                {
                    l.Id,
                    l.BloggId,
                    BloggTitle = bloggTitles.TryGetValue(l.BloggId, out var t2) ? t2 : "",
                    l.CreatedAt
                })
            };

            var json = System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var safeUser = (user.UserName ?? user.Email ?? "user").Replace('@', '_').Replace(':', '_');
            var fileName = $"{safeUser}_personal_data.json";

            return (bytes, fileName, "application/json");
        }
    }
}
