// GNOME Shell extension for Veya — GNOME 45+ (ES modules, GJS)
// Thin D-Bus client of org.veya.Veya1. No intelligence on this side.

import St from 'gi://St';
import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import Clutter from 'gi://Clutter';
import Shell from 'gi://Shell';
import Meta from 'gi://Meta';

import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import { Extension } from 'resource:///org/gnome/shell/extensions/extension.js';

const DBUS_NAME = 'org.veya.Veya1';
const DBUS_PATH = '/org/veya/Veya1';
const DBUS_IFACE = 'org.veya.Veya1';
const ASK_TIMEOUT_MS = 60_000;

export default class VeyaExtension extends Extension {
    _panel = null;
    _entry = null;
    _spinner = null;
    _cloudBadge = null;
    _reply = null;
    _cancellable = null;
    _cloudSignalSubId = null;
    _keyPressId = null;
    _settings = null;

    enable() {
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
    }

    disable() {
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

        this._panel?.destroy();
        this._panel = null;
        this._entry = null;
        this._spinner = null;
        this._cloudBadge = null;
        this._reply = null;
        this._settings = null;
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    _buildPanel() {
        this._panel = new St.BoxLayout({
            style_class: 'veya-panel',
            vertical: true,
            visible: false,
            width: 620,
            reactive: true,
        });

        this._entry = new St.Entry({
            style_class: 'veya-entry',
            hint_text: 'Ask Veya…',
            can_focus: true,
            x_expand: true,
        });
        this._entry.clutter_text.connect('activate', () => this._submit());

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
        this._reply.clutter_text.set_line_wrap_mode(imports.gi.Pango.WrapMode.WORD_CHAR);

        this._panel.add_child(this._entry);
        this._panel.add_child(statusRow);
        this._panel.add_child(this._reply);

        Main.uiGroup.add_child(this._panel);

        this._panel.connect('notify::width', () => this._reposition());
        this._panel.connect('notify::height', () => this._reposition());

        this._keyPressId = global.stage.connect('key-press-event', (_actor, event) => {
            if (this._panel.visible && event.get_key_symbol() === Clutter.KEY_Escape) {
                this._hide();
                return Clutter.EVENT_STOP;
            }
            return Clutter.EVENT_PROPAGATE;
        });
    }

    _reposition() {
        const monitor = Main.layoutManager.primaryMonitor;
        if (!monitor) return;
        const panelH = Main.panel?.height ?? 0;
        this._panel.set_position(
            monitor.x + Math.floor((monitor.width - this._panel.width) / 2),
            monitor.y + panelH + 48,
        );
    }

    _toggle() {
        if (this._panel.visible)
            this._hide();
        else
            this._show();
    }

    _show() {
        this._entry.set_text('');
        this._reply.set_text('');
        this._reply.visible = false;
        this._cloudBadge.visible = false;
        this._spinner.visible = false;
        this._panel.visible = true;
        this._reposition();
        this._entry.grab_key_focus();
    }

    _hide() {
        this._panel.visible = false;
        this._cancellable?.cancel();
        this._cancellable = null;
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

    _showReply(text) {
        this._reply.set_text(text);
        this._reply.visible = true;
    }
}
