using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordex.Core.Common;

namespace Ordex.Web.Controllers;

public static class ToastKeys
{
    public const string Success = "toast.success";
    public const string Error = "toast.error";
}

/// <summary>
/// Base for every signed-in page. Holds the small, repeated bits
/// (toasts, service-result handling, safe redirects) so controllers stay thin.
/// </summary>
[Authorize]
public abstract class AppController : Controller
{
    protected void ToastSuccess(string message) => TempData[ToastKeys.Success] = message;

    protected void ToastError(string message) => TempData[ToastKeys.Error] = message;

    /// <summary>
    /// True when the service call succeeded. Otherwise puts the error on the form
    /// (shown in the validation summary) and returns false.
    /// </summary>
    protected bool Succeeded(ServiceResult result)
    {
        if (result.Succeeded) return true;
        ModelState.AddModelError(string.Empty, result.Error ?? Messages.UnexpectedError);
        return false;
    }

    /// <summary>For quick actions (buttons / modals): toast the outcome and go back.</summary>
    protected IActionResult ToastAndReturn(ServiceResult result, string successMessage, string? returnUrl, string fallbackAction = "Index")
    {
        if (result.Succeeded)
            ToastSuccess(successMessage);
        else
            ToastError(result.Error ?? Messages.UnexpectedError);

        return RedirectToLocal(returnUrl, fallbackAction);
    }

    /// <summary>Invalid modal form → toast the first validation message and go back.</summary>
    protected IActionResult InvalidAndReturn(string? returnUrl, string fallbackAction = "Index")
    {
        ToastError(FirstModelError());
        return RedirectToLocal(returnUrl, fallbackAction);
    }

    /// <summary>Only redirects inside this site (prevents open-redirect attacks).</summary>
    protected IActionResult RedirectToLocal(string? returnUrl, string fallbackAction = "Index", string? fallbackController = null) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(fallbackAction, fallbackController);

    protected string FirstModelError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
        ?? Messages.ValidationFailed;
}
