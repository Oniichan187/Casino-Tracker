namespace CasinoTracker.Services;

/// <summary>
/// Android implementation: the system "save as" dialog (Storage Access Framework, ACTION_CREATE_DOCUMENT)
/// for exports and MAUI's <see cref="FilePicker"/> for imports. Neither needs a storage permission, because
/// the user hands the app one specific file.
/// </summary>
public sealed class FileTransferService : IFileTransferService
{
    private const int CreateDocumentRequestCode = 0x4354; // arbitrary, must not clash with other requests

    // Only one "save as" dialog can be open at a time.
    private static TaskCompletionSource<Android.Net.Uri?>? _pendingCreate;

    public async Task<bool> SaveAsync(string sourcePath, string suggestedFileName)
    {
        var target = await RequestCreateDocumentAsync(suggestedFileName);
        if (target is null) return false;

        var resolver = Android.App.Application.Context.ContentResolver
                       ?? throw new InvalidOperationException("The Android content resolver is not available.");

        try
        {
            await using var input = File.OpenRead(sourcePath);
            // "wt" = write + truncate, so saving over an existing longer file leaves no trailing bytes behind.
            await using var output = resolver.OpenOutputStream(target, "wt")
                                     ?? throw new IOException("The selected location cannot be written to.");
            await input.CopyToAsync(output);
            await output.FlushAsync();
        }
        catch
        {
            // Do not leave an empty or half written file behind that looks like a valid backup.
            try
            {
                Android.Provider.DocumentsContract.DeleteDocument(resolver, target);
            }
            catch
            {
                // best effort
            }
            throw;
        }

        return true;
    }

    public async Task<string?> PickAsync()
    {
        // No file type filter: the backup has no registered MIME type. The content is validated afterwards.
        var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a Casino Tracker backup" });
        if (picked is null) return null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"casinotracker-import-{Guid.NewGuid():N}.db3");
        try
        {
            await using var input = await picked.OpenReadAsync();
            await using var output = File.Create(tempPath);
            await input.CopyToAsync(output);
        }
        catch
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // best effort
            }
            throw;
        }

        return tempPath;
    }

    private static Task<Android.Net.Uri?> RequestCreateDocumentAsync(string fileName)
    {
        var activity = Platform.CurrentActivity
                       ?? throw new InvalidOperationException("There is no active screen to show the file dialog on.");

        var completion = new TaskCompletionSource<Android.Net.Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _pendingCreate, completion)?.TrySetResult(null); // release an unanswered older request

        var intent = new Android.Content.Intent(Android.Content.Intent.ActionCreateDocument);
        intent.AddCategory(Android.Content.Intent.CategoryOpenable);
        intent.SetType("application/octet-stream"); // keeps the file manager from appending another extension
        intent.PutExtra(Android.Content.Intent.ExtraTitle, fileName);
        activity.StartActivityForResult(intent, CreateDocumentRequestCode);

        return completion.Task;
    }

    /// <summary>Called by <c>MainActivity.OnActivityResult</c>; completes the pending "save as" request.</summary>
    internal static void HandleActivityResult(int requestCode, Android.App.Result resultCode, Android.Content.Intent? data)
    {
        if (requestCode != CreateDocumentRequestCode) return;

        var pending = Interlocked.Exchange(ref _pendingCreate, null);
        pending?.TrySetResult(resultCode == Android.App.Result.Ok ? data?.Data : null);
    }
}
