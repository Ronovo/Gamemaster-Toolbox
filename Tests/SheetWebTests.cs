using System.IO.Compression;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Tests;

public class SheetWebTests(AppFactory app) : IClassFixture<AppFactory>
{
    private static async Task<string> UploadAsync(HttpClient client, string fileName, string contents) =>
        WebUtility.HtmlDecode(await (await AppFactory.UploadSheetAsync(client, null, fileName, contents)).Content.ReadAsStringAsync());

    private int SheetId(string fileName) =>
        app.Query(db => db.Sheets.IgnoreQueryFilters().Where(s => s.FileName == fileName).Select(s => s.ID).Single());

    [Fact]
    public async Task Uploading_the_same_name_again_adds_a_version_only_when_the_file_changed()
    {
        var client = await app.LoggedInAsync("sheet-uploader");
        Assert.Contains("Uploaded \"rogue.pdf\" as version 1.", await UploadAsync(client, "rogue.pdf", "%PDF-1.4 first"));
        Assert.Contains("\"rogue.pdf\" is unchanged, still at version 1.", await UploadAsync(client, "Rogue.pdf", "%PDF-1.4 first"));
        Assert.Contains("\"rogue.pdf\" updated to version 2.", await UploadAsync(client, "rogue.pdf", "%PDF-1.4 second"));
        var id = SheetId("rogue.pdf");
        Assert.Contains("rogue.pdf", await client.GetStringAsync($"/Sheets/Details/{id}"));

        var current = await client.GetAsync($"/Sheets/Download/{id}");
        Assert.Equal("rogue.pdf", current.Content.Headers.ContentDisposition!.FileNameStar ?? current.Content.Headers.ContentDisposition.FileName);
        Assert.Equal("%PDF-1.4 second", await current.Content.ReadAsStringAsync());
        var first = await client.GetAsync($"/Sheets/Download/{id}?version=1");
        Assert.Equal("rogue (v1).pdf", first.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.Equal("%PDF-1.4 first", await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Sheets/Download/{id}?version=9")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Sheets/Download/999999")).StatusCode);
    }

    [Fact]
    public async Task An_upload_with_no_file_or_a_file_that_is_not_a_pdf_is_refused()
    {
        var client = await app.LoggedInAsync("sheet-refused");
        var form = new MultipartFormDataContent { { new StringContent(await AppFactory.TokenAsync(client, "/Sheets/Upload")), "__RequestVerificationToken" } };
        Assert.Contains("Choose a file to upload.", await (await client.PostAsync("/Sheets/Upload", form)).Content.ReadAsStringAsync());
        var notPdf = await UploadAsync(client, "notes.pdf", "just some text");
        Assert.Contains("validation-summary-errors", notPdf);
        Assert.False(app.Query(db => db.Sheets.IgnoreQueryFilters().Any(s => s.FileName == "notes.pdf")));
    }

    [Fact]
    public async Task Every_sheet_downloads_together_as_a_zip()
    {
        var client = await app.LoggedInAsync("sheet-zipper");
        await UploadAsync(client, "fighter.pdf", "%PDF-1.4 fighter");
        await UploadAsync(client, "wizard.pdf", "%PDF-1.4 wizard");
        var zip = await client.GetAsync("/Sheets/ExportAll");
        Assert.Equal("application/zip", zip.Content.Headers.ContentType!.MediaType);
        using var archive = new ZipArchive(await zip.Content.ReadAsStreamAsync());
        Assert.Equal(["fighter.pdf", "wizard.pdf"], archive.Entries.Select(e => e.FullName).Order().ToList());
    }

    [Fact]
    public async Task A_sheet_or_an_old_version_can_be_deleted_but_not_the_current_version_alone()
    {
        var client = await app.LoggedInAsync("sheet-deleter");
        await UploadAsync(client, "cleric.pdf", "%PDF-1.4 one");
        await UploadAsync(client, "cleric.pdf", "%PDF-1.4 two");
        var id = SheetId("cleric.pdf");
        var details = $"/Sheets/Details/{id}";

        Assert.Contains("cleric.pdf", await client.GetStringAsync($"/Sheets/DeleteVersion/{id}?version=1"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Sheets/DeleteVersion/{id}?version=9")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Sheets/DeleteVersion/999999?version=1")).StatusCode);

        var refused = await AppFactory.PostAsync(client, $"/Sheets/DeleteVersion/{id}?version=2", details, new());
        Assert.Contains("The current version can't be deleted on its own.", WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync()));
        await AppFactory.PostAsync(client, $"/Sheets/DeleteVersion/{id}?version=9", details, new());
        await AppFactory.PostAsync(client, $"/Sheets/DeleteVersion/{id}?version=1", details, new());
        Assert.Equal([2], app.Query(db => db.SheetVersions.IgnoreQueryFilters().Where(v => v.SheetID == id).Select(v => v.Version).ToList()));
        var gone = await AppFactory.PostAsync(client, "/Sheets/DeleteVersion/999999?version=1", details, new());
        Assert.Equal("/Sheets", gone.RequestMessage!.RequestUri!.AbsolutePath);

        Assert.Contains("cleric.pdf", await client.GetStringAsync($"/Sheets/Delete/{id}"));
        await AppFactory.PostAsync(client, $"/Sheets/Delete/{id}", details, new());
        Assert.False(app.Query(db => db.Sheets.IgnoreQueryFilters().Any(s => s.ID == id)));
        Assert.False(Directory.Exists(Path.Combine(app.SheetsRoot, id.ToString())) && Directory.EnumerateFiles(Path.Combine(app.SheetsRoot, id.ToString())).Any());
        var again = await AppFactory.PostAsync(client, $"/Sheets/Delete/{id}", "/Sheets", new());
        Assert.Equal("/Sheets", again.RequestMessage!.RequestUri!.AbsolutePath);

        foreach (var page in new[] { "Details", "Delete" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Sheets/{page}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Sheets/{page}/999999")).StatusCode);
        }
    }

    [Fact]
    public async Task The_list_filters_by_name_and_answers_with_just_the_table_when_asked()
    {
        var client = await app.LoggedInAsync("sheet-lister");
        await UploadAsync(client, "bard.pdf", "%PDF-1.4 bard");
        await UploadAsync(client, "monk.pdf", "%PDF-1.4 monk");
        var partial = await client.GetStringAsync("/Sheets?name=bard&partial=1");
        Assert.Contains("bard.pdf", partial);
        Assert.DoesNotContain("monk.pdf", partial);
        Assert.DoesNotContain("<html", partial);
    }
}
