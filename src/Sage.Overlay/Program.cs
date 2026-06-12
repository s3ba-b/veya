using Sage.Overlay;

var client = new Sage1Client();
var viewModel = new OverlayViewModel(client);

var app = Adw.Application.New("org.sage.Overlay", Gio.ApplicationFlags.FlagsNone);
app.OnActivate += (sender, _) =>
{
    var window = OverlayWindow.Create((Adw.Application)sender, viewModel);
    window.Show();
};

var exitCode = app.RunWithSynchronizationContext(null);
await client.DisposeAsync();
return exitCode;
