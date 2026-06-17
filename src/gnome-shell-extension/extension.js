// GNOME Shell extension for Veya — GNOME 45+ (ES modules, GJS)
// Thin D-Bus client of org.veya.Veya1. No intelligence on this side.

import St from 'gi://St';
import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import Clutter from 'gi://Clutter';
import Shell from 'gi://Shell';
import Meta from 'gi://Meta';
import Pango from 'gi://Pango';

import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import * as PanelMenu from 'resource:///org/gnome/shell/ui/panelMenu.js';
import { Extension } from 'resource:///org/gnome/shell/extensions/extension.js';

const DBUS_NAME = 'org.veya.Veya1';
const DBUS_PATH = '/org/veya/Veya1';
const DBUS_IFACE = 'org.veya.Veya1';
const ASK_TIMEOUT_MS = 60_000;
// AskVoice blocks server-side for the recording window plus transcription,
// answering, and speaking — give it more headroom than a typed Ask.
const VOICE_MAX_DURATION_MS = 8_000;
const VOICE_CALL_TIMEOUT_MS = VOICE_MAX_DURATION_MS + ASK_TIMEOUT_MS;

export default class VeyaExtension extends Extension {
    _panel = null;
    _entry = null;
    _micButton = null;
    _spinner = null;
    _cloudBadge = null;
    _reply = null;
    _cancellable = null;
    _cloudSignalSubId = null;
    _keyPressId = null;
    _settings = null;
    _indicator = null;

    enable() {
        log('Veya: enable()');
        this._settings = this.getSettings();
        this._buildPanel();
        this._connectCloudSignal();

        Main.wm.addKeybinding(
            'summon-shortcut',
            this._settings,
            Meta.KeyBindingFlags.IGNORE_AUTOREPEAT,
            Shell.ActionMode.NORMAL | Shell.ActionMode.OVERVIEW,
            () => this._toggle(),
        );
        this._addPanelButton();
        log('Veya: enable() done, keybinding registered');
    }

    disable() {
        log('Veya: disable()');
        Main.wm.removeKeybinding('summon-shortcut');

        if (this._cloudSignalSubId !== null) {
            Gio.DBus.session.signal_unsubscribe(this._cloudSignalSubId);
            this._cloudSignalSubId = null;
        }

        if (this._keyPressId !== null) {
            global.stage.disconnect(this._keyPressId);
            this._keyPressId = null;
        }

        this._cancellable?.cancel();
        this._cancellable = null;

        this._indicator?.destroy();
        this._indicator = null;

        if (this._panel) {
            Main.layoutManager.removeChrome(this._panel);
            this._panel.destroy();
            this._panel = null;
        }
        this._entry = null;
        this._micButton = null;
        this._spinner = null;
        this._cloudBadge = null;
        this._reply = null;
        this._settings = null;
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    _buildPanel() {
        log('Veya: _buildPanel()');
        this._panel = new St.BoxLayout({
            style_class: 'veya-panel',
            vertical: true,
            visible: false,
            reactive: true,
            track_hover: true,
        });

        this._entry = new St.Entry({
            style_class: 'veya-entry',
            hint_text: 'Ask Veya…',
            can_focus: true,
            x_expand: true,
        });
        this._entry.clutter_text.connect('activate', () => this._submit());

        this._micButton = new St.Button({
            style_class: 'veya-mic-button',
            child: new St.Icon({ icon_name: 'audio-input-microphone-symbolic', icon_size: 16 }),
            can_focus: true,
        });
        this._micButton.connect('clicked', () => this._submitVoice());

        const inputRow = new St.BoxLayout({ style_class: 'veya-input-row' });
        inputRow.add_child(this._entry);
        inputRow.add_child(this._micButton);

        const statusRow = new St.BoxLayout({ style_class: 'veya-status-row' });

        this._spinner = new St.Label({
            style_class: 'veya-spinner',
            text: 'Thinking…',
            visible: false,
        });

        this._cloudBadge = new St.Label({
            style_class: 'veya-cloud-badge',
            text: '☁ cloud',
            visible: false,
        });

        statusRow.add_child(this._spinner);
        statusRow.add_child(this._cloudBadge);

        this._reply = new St.Label({
            style_class: 'veya-reply',
            text: '',
            visible: false,
            x_expand: true,
        });
        this._reply.clutter_text.set_line_wrap(true);
        this._reply.clutter_text.set_selectable(true);
        this._reply.clutter_text.set_line_wrap_mode(Pango.WrapMode.WORD_CHAR);

        this._panel.add_child(inputRow);
        this._panel.add_child(statusRow);
        this._panel.add_child(this._reply);

        Main.layoutManager.addTopChrome(this._panel);

        this._keyPressId = global.stage.connect('key-press-event', (_actor, event) => {
            if (this._panel.visible && event.get_key_symbol() === Clutter.KEY_Escape) {
                this._hide();
                return Clutter.EVENT_STOP;
            }
            return Clutter.EVENT_PROPAGATE;
        });

        log('Veya: _buildPanel() done');
    }

    _reposition() {
        const monitor = Main.layoutManager.primaryMonitor;
        if (!monitor) return;
        const panelH = Main.panel?.height ?? 0;
        const panelW = 620;
        const x = monitor.x + Math.floor((monitor.width - panelW) / 2);
        const y = monitor.y + panelH + 48;
        log(`Veya: reposition to ${x},${y} (monitor ${monitor.width}x${monitor.height})`);
        this._panel.set_position(x, y);
        this._panel.set_width(panelW);
    }

    _toggle() {
        log(`Veya: _toggle(), panel visible=${this._panel?.visible}`);
        try {
            if (this._panel.visible)
                this._hide();
            else
                this._show();
        } catch (e) {
            logError(e, 'Veya: _toggle failed');
        }
    }

    _show() {
        log('Veya: _show()');
        this._entry.set_text('');
        this._reply.set_text('');
        this._reply.visible = false;
        this._cloudBadge.visible = false;
        this._spinner.visible = false;
        this._reposition();
        this._panel.visible = true;
        this._entry.grab_key_focus();
        log('Veya: _show() done');
    }

    _hide() {
        log('Veya: _hide()');
        this._panel.visible = false;
        this._cancellable?.cancel();
        this._cancellable = null;
    }

    _addPanelButton() {
        this._indicator = new PanelMenu.Button(0.0, 'Veya', true);
        const icon = new St.Icon({
            icon_name: 'edit-find-symbolic',
            style_class: 'system-status-icon',
        });
        this._indicator.add_child(icon);
        this._indicator.connect('button-press-event', (_actor, event) => {
            if (event.get_button() === Clutter.BUTTON_PRIMARY) {
                log('Veya: panel button clicked');
                this._toggle();
                return Clutter.EVENT_STOP;
            }
            return Clutter.EVENT_PROPAGATE;
        });
        Main.panel.addToStatusArea('veya', this._indicator, 1, 'right');
        log('Veya: panel button added');
    }

    // ── D-Bus ────────────────────────────────────────────────────────────────

    _connectCloudSignal() {
        this._cloudSignalSubId = Gio.DBus.session.signal_subscribe(
            null,
            DBUS_IFACE,
            'CloudUsage',
            DBUS_PATH,
            null,
            Gio.DBusSignalFlags.NONE,
            () => {
                if (this._panel.visible)
                    this._cloudBadge.visible = true;
            },
        );
    }

    _submit() {
        this._submitAsync().catch(e => {
            if (!e.matches(Gio.IOErrorEnum, Gio.IOErrorEnum.CANCELLED)) {
                logError(e, 'Veya extension: Ask failed');
                this._showReply(`Error: ${e.message}`);
            }
        });
    }

    async _submitAsync() {
        const prompt = this._entry.get_text().trim();
        if (!prompt) return;

        this._entry.reactive = false;
        this._micButton.reactive = false;
        this._spinner.visible = true;
        this._reply.visible = false;
        this._cloudBadge.visible = false;

        this._cancellable?.cancel();
        this._cancellable = new Gio.Cancellable();

        try {
            const reply = await this._ask(prompt, this._cancellable);
            this._showReply(reply);
        } finally {
            this._spinner.visible = false;
            this._entry.reactive = true;
            this._micButton.reactive = true;
        }
    }

    _ask(prompt, cancellable) {
        return new Promise((resolve, reject) => {
            Gio.DBus.session.call(
                DBUS_NAME,
                DBUS_PATH,
                DBUS_IFACE,
                'Ask',
                new GLib.Variant('(s)', [prompt]),
                new GLib.VariantType('(s)'),
                Gio.DBusCallFlags.NONE,
                ASK_TIMEOUT_MS,
                cancellable,
                (conn, result) => {
                    try {
                        const [reply] = conn.call_finish(result).deep_unpack();
                        resolve(reply);
                    } catch (e) {
                        reject(e);
                    }
                },
            );
        });
    }

    _submitVoice() {
        this._submitVoiceAsync().catch(e => {
            if (!e.matches(Gio.IOErrorEnum, Gio.IOErrorEnum.CANCELLED)) {
                logError(e, 'Veya extension: AskVoice failed');
                this._showReply(`Error: ${e.message}`);
            }
        });
    }

    async _submitVoiceAsync() {
        this._entry.reactive = false;
        this._micButton.reactive = false;
        this._spinner.text = 'Listening…';
        this._spinner.visible = true;
        this._reply.visible = false;
        this._cloudBadge.visible = false;

        this._cancellable?.cancel();
        this._cancellable = new Gio.Cancellable();

        try {
            const { transcript, reply } = await this._askVoice(VOICE_MAX_DURATION_MS, this._cancellable);
            this._showReply(transcript ? `🎤 "${transcript}"\n\n${reply}` : reply);
        } finally {
            this._spinner.visible = false;
            this._spinner.text = 'Thinking…';
            this._entry.reactive = true;
            this._micButton.reactive = true;
        }
    }

    _askVoice(maxDurationMs, cancellable) {
        return new Promise((resolve, reject) => {
            Gio.DBus.session.call(
                DBUS_NAME,
                DBUS_PATH,
                DBUS_IFACE,
                'AskVoice',
                new GLib.Variant('(u)', [maxDurationMs]),
                new GLib.VariantType('(ss)'),
                Gio.DBusCallFlags.NONE,
                VOICE_CALL_TIMEOUT_MS,
                cancellable,
                (conn, result) => {
                    try {
                        const [transcript, reply] = conn.call_finish(result).deep_unpack();
                        resolve({ transcript, reply });
                    } catch (e) {
                        reject(e);
                    }
                },
            );
        });
    }

    _showReply(text) {
        this._reply.set_text(text);
        this._reply.visible = true;
    }
}
