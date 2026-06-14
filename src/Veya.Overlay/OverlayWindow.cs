using Adw;
using Gtk;

namespace Veya.Overlay;

/// <summary>
/// Builds the overlay's window: a header bar, a prompt entry, and a
/// scrollable response area. All Veya logic lives in
/// <see cref="OverlayViewModel"/>; this class only wires GTK widgets to it
/// (ADR-0002).
/// </summary>
public static class OverlayWindow
{
    public static Adw.ApplicationWindow Create(Adw.Application app, OverlayViewModel viewModel)
    {
        var window = Adw.ApplicationWindow.New(app);
        window.SetTitle("Veya");
        window.SetDefaultSize(480, 360);

        var headerBar = Adw.HeaderBar.New();

        var toolbarView = Adw.ToolbarView.New();
        toolbarView.AddTopBar(headerBar);

        var entry = Gtk.Entry.New();
        entry.SetPlaceholderText("Ask Veya...");
        entry.SetMarginStart(12);
        entry.SetMarginEnd(12);
        entry.SetMarginTop(12);

        var responseLabel = Gtk.Label.New(string.Empty);
        responseLabel.SetWrap(true);
        responseLabel.SetXalign(0);
        responseLabel.SetValign(Align.Start);
        responseLabel.SetMarginStart(12);
        responseLabel.SetMarginEnd(12);
        responseLabel.SetMarginBottom(12);

        var scroller = Gtk.ScrolledWindow.New();
        scroller.SetChild(responseLabel);
        scroller.SetVexpand(true);

        var box = Gtk.Box.New(Orientation.Vertical, 0);
        box.Append(entry);
        box.Append(scroller);

        entry.OnActivate += async (_, _) =>
        {
            var prompt = entry.GetText();
            entry.SetSensitive(false);
            responseLabel.SetText("Thinking…");

            var reply = await viewModel.AskAsync(prompt).ConfigureAwait(true);

            responseLabel.SetText(reply);
            entry.SetSensitive(true);
            entry.GrabFocus();
        };

        toolbarView.SetContent(box);
        window.SetContent(toolbarView);

        return window;
    }
}
