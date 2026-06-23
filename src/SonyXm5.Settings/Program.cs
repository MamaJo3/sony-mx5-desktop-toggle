using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SonyXm5.Core;

// Settings window for the XM5 ambient daemon. Edits config.json and restarts the daemon.
static class ConfigApp
{
    static string Dir => AppContext.BaseDirectory;
    static string CfgFile => Path.Combine(Dir, "config.json");
    static string DaemonExe => Path.Combine(Dir, "sony-ambient-daemon.exe");

    static readonly string[] Codes = { "amb", "nc", "off", "wind" };
    static readonly string[] Names = { "Ambient Sound", "Noise Cancelling", "Off", "Wind Noise Reduction" };
    static int CodeIndex(string c) { for (int i = 0; i < Codes.Length; i++) if (Codes[i] == c) return i; return 0; }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    public class MainForm : Form
    {
        TextBox txtHotkey; RadioButton rbToggle, rbHold;
        Label lblA, lblB, lblLevelVal, lblStatus;
        ComboBox cmbA, cmbB; TrackBar trkLevel; string _hotkey = "CTRL+ALT+A";

        public MainForm()
        {
            Text = "Sony XM5 Ambient — Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 360); Font = new Font("Segoe UI", 9f);

            Add(new Label { Text = "Shortcut:", Location = new Point(14, 18), AutoSize = true });
            txtHotkey = new TextBox { Location = new Point(120, 15), Width = 230, ReadOnly = true, BackColor = Color.White, Cursor = Cursors.Hand, Text = _hotkey };
            txtHotkey.KeyDown += CaptureHotkey;
            txtHotkey.Enter += (s, e) => { txtHotkey.BackColor = Color.LightYellow; SetStatus("Press your key combo now (needs Ctrl/Alt/Shift)…"); };
            txtHotkey.Leave += (s, e) => txtHotkey.BackColor = Color.White;
            Add(txtHotkey);
            Add(new Label { Text = "(click, then press the keys)", Location = new Point(120, 41), AutoSize = true, ForeColor = Color.Gray });

            var grp = new GroupBox { Text = "Behavior", Location = new Point(14, 66), Size = new Size(432, 78) };
            rbToggle = new RadioButton { Text = "Toggle  —  each press switches between the two modes", Location = new Point(14, 22), AutoSize = true, Checked = true };
            rbHold = new RadioButton { Text = "Hold  —  first mode while the key is held, second on release", Location = new Point(14, 47), AutoSize = true };
            rbToggle.CheckedChanged += (s, e) => RelabelModes();
            grp.Controls.Add(rbToggle); grp.Controls.Add(rbHold); Add(grp);

            lblA = new Label { Location = new Point(14, 158), AutoSize = true };
            cmbA = new ComboBox { Location = new Point(150, 155), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            lblB = new Label { Location = new Point(14, 190), AutoSize = true };
            cmbB = new ComboBox { Location = new Point(150, 187), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbA.Items.AddRange(Names); cmbB.Items.AddRange(Names);
            cmbA.SelectedIndexChanged += (s, e) => UpdateLevelEnabled();
            cmbB.SelectedIndexChanged += (s, e) => UpdateLevelEnabled();
            Add(lblA); Add(cmbA); Add(lblB); Add(cmbB);

            Add(new Label { Text = "Ambient level:", Location = new Point(14, 228), AutoSize = true });
            trkLevel = new TrackBar { Location = new Point(120, 222), Width = 230, Minimum = 0, Maximum = 20, TickFrequency = 5, Value = 20 };
            trkLevel.ValueChanged += (s, e) => lblLevelVal.Text = trkLevel.Value.ToString();
            lblLevelVal = new Label { Location = new Point(356, 228), AutoSize = true, Text = "20" };
            Add(trkLevel); Add(lblLevelVal);

            var btnSave = new Button { Text = "Save && Apply", Location = new Point(120, 285), Size = new Size(130, 32) };
            btnSave.Click += (s, e) => Save();
            var btnClose = new Button { Text = "Close", Location = new Point(260, 285), Size = new Size(90, 32) };
            btnClose.Click += (s, e) => Close();
            Add(btnSave); Add(btnClose);

            lblStatus = new Label { Location = new Point(14, 325), AutoSize = false, Size = new Size(432, 30), ForeColor = Color.DimGray };
            Add(lblStatus);

            LoadInto();
        }

        void Add(Control c) => Controls.Add(c);
        void SetStatus(string s) { lblStatus.Text = s; }

        void RelabelModes()
        {
            if (rbHold.Checked) { lblA.Text = "While held:"; lblB.Text = "On release:"; }
            else { lblA.Text = "Mode 1:"; lblB.Text = "Mode 2:"; }
        }
        void UpdateLevelEnabled()
        {
            bool amb = (cmbA.SelectedIndex >= 0 && Codes[cmbA.SelectedIndex] == "amb") || (cmbB.SelectedIndex >= 0 && Codes[cmbB.SelectedIndex] == "amb");
            trkLevel.Enabled = amb; lblLevelVal.Enabled = amb;
        }

        void CaptureHotkey(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true; e.Handled = true;
            var k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.Menu || k == Keys.ShiftKey || k == Keys.LWin || k == Keys.RWin) return;
            string key = KeyToken(k);
            if (key == null) { SetStatus("Unsupported key — try a letter, number, F-key, etc."); return; }
            var parts = new List<string>();
            if (e.Control) parts.Add("CTRL");
            if (e.Alt) parts.Add("ALT");
            if (e.Shift) parts.Add("SHIFT");
            if (parts.Count == 0) { SetStatus("Add a modifier: hold Ctrl, Alt, or Shift with the key."); return; }
            parts.Add(key);
            _hotkey = string.Join("+", parts);
            txtHotkey.Text = _hotkey;
            SetStatus("Shortcut set to " + _hotkey);
        }

        static string KeyToken(Keys k)
        {
            if (k >= Keys.A && k <= Keys.Z) return ((char)('A' + (k - Keys.A))).ToString();
            if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return ((char)('0' + (k - Keys.NumPad0))).ToString();
            if (k >= Keys.F1 && k <= Keys.F24) return "F" + (k - Keys.F1 + 1);
            switch (k)
            {
                case Keys.Space: return "SPACE"; case Keys.Return: return "ENTER"; case Keys.Tab: return "TAB"; case Keys.Escape: return "ESC";
                case Keys.Insert: return "INSERT"; case Keys.Delete: return "DELETE"; case Keys.Home: return "HOME"; case Keys.End: return "END";
                case Keys.PageUp: return "PAGEUP"; case Keys.PageDown: return "PAGEDOWN";
                case Keys.Up: return "UP"; case Keys.Down: return "DOWN"; case Keys.Left: return "LEFT"; case Keys.Right: return "RIGHT";
            }
            return null;
        }

        void LoadInto()
        {
            var c = AppConfig.Load(CfgFile);
            _hotkey = string.IsNullOrWhiteSpace(c.hotkey) ? "CTRL+ALT+A" : c.hotkey;
            txtHotkey.Text = _hotkey;
            rbHold.Checked = c.behavior == "hold"; rbToggle.Checked = !rbHold.Checked;
            cmbA.SelectedIndex = CodeIndex(c.modeA); cmbB.SelectedIndex = CodeIndex(c.modeB);
            trkLevel.Value = Math.Max(0, Math.Min(20, c.ambientLevel)); lblLevelVal.Text = trkLevel.Value.ToString();
            RelabelModes(); UpdateLevelEnabled();
            SetStatus("Loaded current settings.");
        }

        void Save()
        {
            if (cmbA.SelectedIndex == cmbB.SelectedIndex) { SetStatus("Pick two different modes for the two slots."); return; }
            var c = new AppConfig
            {
                hotkey = _hotkey,
                behavior = rbHold.Checked ? "hold" : "toggle",
                modeA = Codes[cmbA.SelectedIndex],
                modeB = Codes[cmbB.SelectedIndex],
                ambientLevel = trkLevel.Value
            };
            try { c.Save(CfgFile); }
            catch (Exception ex) { SetStatus("Could not save: " + ex.Message); return; }

            try
            {
                foreach (var p in Process.GetProcessesByName("sony-ambient-daemon")) { try { p.Kill(); p.WaitForExit(2000); } catch { } }
                if (File.Exists(DaemonExe)) Process.Start(new ProcessStartInfo(DaemonExe) { WorkingDirectory = Dir, UseShellExecute = true });
                SetStatus($"Saved & applied. Shortcut: {c.hotkey}  ({c.behavior}).");
            }
            catch (Exception ex) { SetStatus("Saved, but couldn't restart daemon: " + ex.Message); }
        }
    }
}
