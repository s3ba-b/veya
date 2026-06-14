using Veya.Overlay;

var client = new Veya1Client();
var viewModel = new OverlayViewModel(client);

var app = Adw.Application.New("org.veya.Overlay", Gio.ApplicationFlags.FlagsNone);
app.OnActivate += (sender, _) =>
{
    var window = OverlayWindow.Create((Adw.Application)sender, viewModel);
    window.Show();
};

var exitCode = app.RunWithSynchronizationContext(null);
await client.DisposeAsync();
return exitCode;
