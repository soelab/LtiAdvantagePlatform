﻿using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using AdvantagePlatform.Data;
using LtiAdvantageLibrary.NetCore.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AdvantagePlatform.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<AdvantagePlatformUser> _signInManager;
        private readonly UserManager<AdvantagePlatformUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            ApplicationDbContext context,
            UserManager<AdvantagePlatformUser> userManager,
            SignInManager<AdvantagePlatformUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (ModelState.IsValid)
            {
                var user = new AdvantagePlatformUser { UserName = Input.Email, Email = Input.Email };
                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    // Create a Platform for the new user and generate keys

                    var keypair = RsaHelper.GenerateRsaKeyPair();
                    var platform = new Platform
                    {
                        UserId = user.Id,
                        PrivateKey = keypair.PrivateKey,
                        PublicKey = keypair.PublicKey,

                        ContactEmail = user.Email,
                        Description = "Auto generated platform",
                        Guid = $"{Request.Host}",
                        ProductFamilyCode = "LTI Advantage Platform",
                        Url = $"{Request.Scheme}://{Request.Host}/"
                    };
                    await _context.Platforms.AddAsync(platform);
                    await _context.SaveChangesAsync();
                    user.PlatformId = platform.Id;
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("Platform created for new account.");

                    // Create a Course for the new user

                    var course = new Course {UserId = user.Id};
                    await _context.Courses.AddAsync(course);
                    await _context.SaveChangesAsync();
                    user.CourseId = course.Id;
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("Course created for new account");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { userId = user.Id, code },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
