using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Drawing;
using System.Text.Json;

namespace SealedJournal;

// CS8618: Fields are initialized in InitializeControls() called from constructor
#pragma warning disable CS8618

public class SealedJournalApp : Form
{
    private TextBox textBox;
    private string filePath;
    private Button viewButton;
    private CheckBox dateEveryLineChk;
    private Button todayButton;
    private bool isViewMode = false;
    private bool _pendingTimestamp = false;
    private ComboBox fileDropdown;
    private string password;
    private AppConfig config;

    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 1;
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SYSMENU_VIEW = 0x9001;
    private const int SYSMENU_TODAY = 0x9002;
    private const int SYSMENU_SETTINGS = 0x9003;
    private const int SYSMENU_ABOUT = 0x9004;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll")]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

    private const int MF_SEPARATOR = 0x800;
    private const int MF_STRING = 0x0;

    public SealedJournalApp()
    {
        config = LoadConfiguration();

        PasswordDialogResult authResult = PasswordDialog.Show("Enter Password:", "Authentication");
        
        if (authResult.Cancelled || string.IsNullOrEmpty(authResult.Password))
        {
            Environment.Exit(0);
        }

        password = authResult.Password;

        InitializeForm();
        InitializeControls();
        SetupHotkey();
        ValidatePasswordAgainstLatestFile();
    }

    private AppConfig LoadConfiguration()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        
        if (File.Exists(configPath))
        {
            try
            {
                string json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        return new AppConfig();
    }

    private void InitializeForm()
    {
        this.Text = "Sealed Journal";
        this.Width = config.Window.Width;
        this.Height = config.Window.Height;

        string notesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.NotesDirectory);
        Directory.CreateDirectory(notesDirectory);
        string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
        filePath = Path.Combine(notesDirectory, fileName);
    }

    private Panel navPanel;
    private Panel buttonPanel;
    private Label fileLabel;
    private GroupBox timestampGroupBox;
    private int minFullWidth = 700;
    private const int LabelButtonWidth = 65;

    private void InitializeControls()
    {
        // Create menu bar
        MenuStrip menuStrip = new MenuStrip();
        
        ToolStripMenuItem actionsMenu = new ToolStripMenuItem("&Actions");
        actionsMenu.DropDownItems.Add("View/Hide", null, ViewButton_Click);
        actionsMenu.DropDownItems.Add("-");
        actionsMenu.DropDownItems.Add("Today", null, TodayButton_Click);
        actionsMenu.DropDownItems.Add("-");
        actionsMenu.DropDownItems.Add("Settings", null, SettingsButton_Click);
        actionsMenu.DropDownItems.Add("About", null, AboutButton_Click);
        actionsMenu.DropDownItems.Add("-");
        actionsMenu.DropDownItems.Add("Exit", null, (s, e) => this.Close());
        menuStrip.Items.Add(actionsMenu);

        this.MainMenuStrip = menuStrip;

        // Add items to system menu (top-left icon)
        this.Load += (s, e) => AddSystemMenuItems();

        // Button panel with Timestamp and View (Dock.Top - added second, appears below menu)
        buttonPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38
        };

        viewButton = new Button
        {
            Text = "View",
            Left = 5,
            Top = 7,
            Width = LabelButtonWidth,
            Height = 24
        };
        viewButton.Click += ViewButton_Click;
        buttonPanel.Controls.Add(viewButton);

        // Timestamp checkbox in a GroupBox for outline
        timestampGroupBox = new GroupBox
        {
            Text = "",
            Left = LabelButtonWidth + 10,
            Top = 0,
            Width = 165,
            Height = 32,
            Anchor = AnchorStyles.Left
        };

        dateEveryLineChk = new CheckBox
        {
            Text = "Timestamp Every Line",
            Left = 5,
            Top = 10,
            AutoSize = true,
            Checked = false
        };
        dateEveryLineChk.CheckedChanged += (s, e) => { if (!dateEveryLineChk.Checked) _pendingTimestamp = false; };
        timestampGroupBox.Controls.Add(dateEveryLineChk);
        buttonPanel.Controls.Add(timestampGroupBox);

        // Navigation panel (Dock.Top)
        navPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30
        };

        fileLabel = new Label
        {
            Text = "Open File:",
            Left = 5,
            Top = 5,
            Width = LabelButtonWidth,
            Anchor = AnchorStyles.Left
        };
        navPanel.Controls.Add(fileLabel);

        fileDropdown = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 70,
            Top = 2,
            Width = 300,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        fileDropdown.SelectedIndexChanged += FileDropdown_SelectedIndexChanged;
        navPanel.Controls.Add(fileDropdown);

        todayButton = new Button
        {
            Text = "Today",
            AutoSize = true,
            Height = 24,
            Anchor = AnchorStyles.Left
        };
        todayButton.Click += TodayButton_Click;
        navPanel.Controls.Add(todayButton);


        // TextBox fills remaining space (Dock.Fill)
        textBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 12),
            AcceptsReturn = true
        };
        textBox.KeyDown += TextBox_KeyDown;
        textBox.KeyPress += TextBox_KeyPress;
        // With Dock.Top, last-added = topmost. Add in reverse of desired visual order:
        // buttonPanel at bottom, navPanel above it, menuStrip at very top.
        this.Controls.Add(textBox);
        this.Controls.Add(buttonPanel);
        this.Controls.Add(navPanel);
        this.Controls.Add(menuStrip);

        // Add today's file even if it doesn't exist
        string todayFileName = DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
        fileDropdown.Items.Add(todayFileName);
        fileDropdown.SelectedItem = todayFileName;
        
        PopulateFileDropdown();

        UpdateWindowTitle();

        if (File.Exists(filePath))
        {
            try
            {
                string encryptedContent = File.ReadAllText(filePath);
                if (!string.IsNullOrWhiteSpace(encryptedContent))
                {
                    string decryptedText = CryptoHelper.Decrypt(encryptedContent, password);
                    string reEncrypted = CryptoHelper.Encrypt(decryptedText, password);
                    File.WriteAllText(filePath, reEncrypted);
                }
            }
            catch
            {
                // File may be from old version or corrupted - will be handled on view
            }
        }

        // Initial layout
        this.Layout += SealedJournalApp_Layout;
        PositionControls();

        // Handle resize end to save window size if RetainWindowSize is enabled
        this.ResizeEnd += SealedJournalApp_ResizeEnd;
    }

    private void SealedJournalApp_ResizeEnd(object? sender, EventArgs e)
    {
        if (config.Window.RetainWindowSize)
        {
            config.Window.Width = this.Width;
            config.Window.Height = this.Height;
            SaveConfiguration();
        }
    }

    private void SaveConfiguration()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(configPath, json);
    }

    private void SealedJournalApp_Layout(object? sender, LayoutEventArgs e)
    {
        PositionControls();
    }

    private void PositionControls()
    {
        int width = this.ClientSize.Width;

        // Size Today button to its text
        int todayWidth = TextRenderer.MeasureText(todayButton.Text, todayButton.Font).Width + 20;
        todayButton.Width = todayWidth;

        if (width < minFullWidth)
        {
            // Compact mode: hide extra controls
            fileLabel.Visible = false;
            todayButton.Visible = false;
            timestampGroupBox.Visible = false;

            fileDropdown.Left = 5;
            fileDropdown.Width = width - 15;
        }
        else
        {
            // Full mode: show all controls
            fileLabel.Visible = true;
            todayButton.Visible = true;
            timestampGroupBox.Visible = true;

            fileLabel.Left = 5;
            fileDropdown.Left = fileLabel.Right + 5;

            todayButton.Left = width - todayWidth - 10;
            fileDropdown.Width = todayButton.Left - fileDropdown.Left - 5;
            if (fileDropdown.Width < 100) fileDropdown.Width = 100;
        }
    }

    private void AddSystemMenuItems()
    {
        IntPtr systemMenu = GetSystemMenu(this.Handle, false);
        if (systemMenu != IntPtr.Zero)
        {
            AppendMenu(systemMenu, MF_SEPARATOR, 0, "");
            AppendMenu(systemMenu, MF_STRING, SYSMENU_VIEW, "View/Hide");
            AppendMenu(systemMenu, MF_STRING, SYSMENU_TODAY, "Today");
            AppendMenu(systemMenu, MF_STRING, SYSMENU_SETTINGS, "Settings");
            AppendMenu(systemMenu, MF_STRING, SYSMENU_ABOUT, "About");
        }
    }

    private void SetupHotkey()
    {
        uint modifiers = 0;
        
        foreach (string mod in config.Hotkey.Modifiers)
        {
            modifiers |= (uint)(mod.ToLower() switch
            {
                "ctrl" => 0x0002,
                "alt" => 0x0001,
                "shift" => 0x0004,
                "win" => 0x0008,
                _ => 0
            });
        }

        uint vk = MapKeyToVirtualKeyCode(config.Hotkey.Key);

        if (vk != 0 && modifiers != 0)
        {
            if (!RegisterHotKey(this.Handle, HOTKEY_ID, modifiers, vk))
            {
                MessageBox.Show("Failed to register hotkey (is another instance already running?)", "Warning");
            }
        }
    }

    private static uint MapKeyToVirtualKeyCode(string keyName)
    {
        return keyName.ToLower() switch
        {
            "a" => 0x41, "b" => 0x42, "c" => 0x43, "d" => 0x44,
            "e" => 0x45, "f" => 0x46, "g" => 0x47, "h" => 0x48,
            "i" => 0x49, "j" => 0x4A, "k" => 0x4B, "l" => 0x4C,
            "m" => 0x4D, "n" => 0x4E, "o" => 0x4F, "p" => 0x50,
            "q" => 0x51, "r" => 0x52, "s" => 0x53, "t" => 0x54,
            "u" => 0x55, "v" => 0x56, "w" => 0x57, "x" => 0x58,
            "y" => 0x59, "z" => 0x5A,
            "f1" => 0x70, "f2" => 0x71, "f3" => 0x72, "f4" => 0x73,
            "f5" => 0x74, "f6" => 0x75, "f7" => 0x76, "f8" => 0x77,
            "f9" => 0x78, "f10" => 0x79, "f11" => 0x7A, "f12" => 0x7B,
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33,
            "4" => 0x34, "5" => 0x35, "6" => 0x36, "7" => 0x37,
            "8" => 0x38, "9" => 0x39,
            "oemminus" or "-" or "numpadsubtract" => 0x6D,
            "oemplus" or "+" or "numpadadd" => 0x6B,
            "numpadmultiply" or "*" => 0x6A,
            "space" => 0x20,
            "return" or "enter" => 0x0D,
            "escape" => 0x1B,
            "tab" => 0x09,
            "insert" => 0x2D,
            "home" => 0x24,
            "delete" => 0x2E,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            _ => 0
        };
    }

    private void ValidatePasswordAgainstLatestFile()
    {
        string notesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.NotesDirectory);
        string[] allFiles = Directory.GetFiles(notesDirectory, "*.txt");
        
        var matchingFiles = allFiles
            .Select(Path.GetFileName)
            .Where(f => f != null && System.Text.RegularExpressions.Regex.IsMatch(f, @"^\d{4}-\d{2}-\d{2}\.txt$"))
            .OrderByDescending(f => f)
            .Cast<string>()
            .ToList();

        if (matchingFiles.Count == 0)
            return;

        string latestFile = Path.Combine(notesDirectory, matchingFiles.First());

        if (File.Exists(latestFile))
        {
            try
            {
                string[] lines = File.ReadAllLines(latestFile);
                string? firstBlock = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                if (firstBlock != null)
                {
                    CryptoHelper.Decrypt(firstBlock, password);
                }
            }
            catch
            {
                DialogResult result = MessageBox.Show(
                    "This password is different from previous notes.\n\n" +
                    "If you continue, today's file will be overwritten with a new password.\n\n" +
                    "Do you want to continue?",
                    "Password Mismatch",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    Environment.Exit(0);
                }
            }
        }
    }

    private void TodayButton_Click(object? sender, EventArgs e)
    {
        string notesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.NotesDirectory);
        string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
        filePath = Path.Combine(notesDirectory, fileName);
        UpdateWindowTitle();
        textBox.Text = string.Empty;
        isViewMode = false;
        _pendingTimestamp = false;

        if (File.Exists(filePath))
        {
            try
            {
                string encryptedContent = File.ReadAllText(filePath);
                if (!string.IsNullOrWhiteSpace(encryptedContent))
                {
                    string decryptedText = CryptoHelper.Decrypt(encryptedContent, password);
                    string reEncrypted = CryptoHelper.Encrypt(decryptedText, password);
                    File.WriteAllText(filePath, reEncrypted);
                }
            }
            catch
            {
                // Handle on view
            }
        }

        if (!fileDropdown.Items.Contains(fileName))
            fileDropdown.Items.Add(fileName);
        fileDropdown.SelectedItem = fileName;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            if (this.Visible && IsApplicationFocused())
            {
                this.Visible = false;
            }
            else
            {
                this.Visible = true;
                ShowWindow(this.Handle, 9);
                SetForegroundWindow(this.Handle);
                this.WindowState = FormWindowState.Normal;
                this.Activate();
                textBox.Focus();
            }
        }
        else if (m.Msg == WM_SYSCOMMAND)
        {
            int cmd = m.WParam.ToInt32();
            switch (cmd)
            {
                case SYSMENU_VIEW:
                    ViewButton_Click(null, EventArgs.Empty);
                    return;
                case SYSMENU_TODAY:
                    TodayButton_Click(null, EventArgs.Empty);
                    return;
                case SYSMENU_SETTINGS:
                    SettingsButton_Click(null, EventArgs.Empty);
                    return;
                case SYSMENU_ABOUT:
                    AboutButton_Click(null, EventArgs.Empty);
                    return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(this.Handle, HOTKEY_ID);
        base.OnFormClosing(e);
    }

    private void TextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift && !isViewMode)
        {
            string visibleText = textBox.Text.Trim();
            if (!string.IsNullOrEmpty(visibleText))
            {
                try
                {
                    string encryptedBlock = CryptoHelper.Encrypt(visibleText, password);
                    File.AppendAllText(filePath, encryptedBlock + Environment.NewLine);
                    textBox.Text = string.Empty;
                    textBox.Select(0, 0);
                    textBox.Focus();

                    _pendingTimestamp = dateEveryLineChk.Checked;

                    e.SuppressKeyPress = true;
                }
                catch
                {
                    MessageBox.Show("Failed to save text. Please try again.", "Error");
                }
            }
        }
        else if (e.Control && e.KeyCode == Keys.E)
        {
            PasswordDialogResult authResult = PasswordDialog.Show("Enter Password:", "Authentication");
            
            if (authResult.Cancelled)
                return;

            if (authResult.Password == password)
            {
                textBox.ReadOnly = !textBox.ReadOnly;
                MessageBox.Show(textBox.ReadOnly ? "Switched to View Mode" : "Switched to Edit Mode");
            }
            else
            {
                MessageBox.Show("Incorrect Password");
            }
        }
        else if (e.KeyCode.ToString().Equals(config.InsertDateKey, StringComparison.OrdinalIgnoreCase))
        {
            _pendingTimestamp = false;
            InsertDate();
        }
    }

    private void ViewButton_Click(object? sender, EventArgs e)
    {
        if (!isViewMode)
        {
            PasswordDialogResult authResult = PasswordDialog.Show("Enter Password:", "Authentication");
            
            if (authResult.Cancelled)
                return;

            if (authResult.Password == password)
            {
                isViewMode = true;
                textBox.ReadOnly = false;
                viewButton.Text = "Hide";

                try
                {
                    string? decryptedText = DecryptFile(filePath, password);
                    textBox.Text = decryptedText ?? string.Empty;
                }
                catch
                {
                    MessageBox.Show("Failed to decrypt the file. The file may be corrupted.", "Error");
                    textBox.Text = string.Empty;
                }
            }
            else
            {
                MessageBox.Show("Incorrect Password");
            }
        }
        else
        {
            isViewMode = false;
            viewButton.Text = "View";

            try
            {
                string fullText = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(fullText))
                {
                    string encryptedBlock = CryptoHelper.Encrypt(fullText, password);
                    File.WriteAllText(filePath, encryptedBlock + Environment.NewLine);
                }
                textBox.Text = string.Empty;
                textBox.ReadOnly = false;
            }
            catch
            {
                MessageBox.Show("Failed to save the file.", "Error");
            }
        }
    }

    private int InsertDate()
    {
        string currentTime = $"[{DateTime.Now:hh:mm:ss tt}] ";
        int cursorPosition = textBox.SelectionStart;
        textBox.Text = textBox.Text.Insert(cursorPosition, currentTime);
        textBox.SelectionStart = cursorPosition + currentTime.Length;
        return currentTime.Length;
    }

    private void TextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_pendingTimestamp && e.KeyChar >= ' ')
        {
            _pendingTimestamp = false;
            string timestamp = $"[{DateTime.Now:hh:mm:ss tt}] ";
            textBox.Text = timestamp;
            textBox.SelectionStart = timestamp.Length;
        }
    }

    private void PopulateFileDropdown()
    {
        string todayFileName = DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
        string notesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.NotesDirectory);
        
        var fileSet = new HashSet<string> { todayFileName };
        
        if (Directory.Exists(notesDirectory))
        {
            string[] files = Directory.GetFiles(notesDirectory, "*.txt");
            foreach (string file in files)
            {
                fileSet.Add(Path.GetFileName(file));
            }
        }

        fileDropdown.Items.Clear();
        foreach (var fileName in fileSet.OrderByDescending(f => f))
        {
            fileDropdown.Items.Add(fileName);
        }

        fileDropdown.SelectedItem = todayFileName;
    }

    private void FileDropdown_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (fileDropdown.SelectedItem != null)
        {
            string notesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.NotesDirectory);
            string? selectedItem = fileDropdown.SelectedItem.ToString();
            if (selectedItem != null)
            {
                string selectedFile = Path.Combine(notesDirectory, selectedItem);

                // Update path even if file doesn't exist yet
                filePath = selectedFile;
                UpdateWindowTitle();
                textBox.Text = string.Empty;
                isViewMode = false;
                _pendingTimestamp = false;
            }
        }
    }

    private void UpdateWindowTitle()
    {
        this.Text = $"Sealed Journal - {Path.GetFileName(filePath)}";
    }

    private static string? DecryptFile(string filePath, string password)
    {
        StringBuilder decryptedText = new StringBuilder();

        try
        {
            string[] encryptedBlocks = File.ReadAllLines(filePath);
            
            foreach (string block in encryptedBlocks)
            {
                if (!string.IsNullOrWhiteSpace(block))
                {
                    decryptedText.AppendLine(CryptoHelper.Decrypt(block, password));
                }
            }

            return decryptedText.ToString().TrimEnd();
        }
        catch
        {
            return null;
        }
    }

    private void AboutButton_Click(object? sender, EventArgs e)
    {
        string aboutText = LoadAboutText();
        MessageBox.Show(aboutText, "About Sealed Journal");
    }

    private static string LoadAboutText()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream("SealedJournal.ABOUT.txt");
        
        if (stream == null)
        {
            return "Sealed Journal - Secure Encrypted Notepad";
        }

        using StreamReader reader = new StreamReader(stream);
        string[] lines = reader.ReadToEnd().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var filteredLines = lines.Where(line => !line.TrimStart().StartsWith("#"));
        return string.Join(Environment.NewLine, filteredLines);
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        SettingsDialog.Show(config);
        
        UnregisterHotKey(this.Handle, HOTKEY_ID);
        SetupHotkey();
        
        this.Width = config.Window.Width;
        this.Height = config.Window.Height;
    }

    public static bool IsApplicationFocused()
    {
        IntPtr activeWindow = GetForegroundWindow();
        GetWindowThreadProcessId(activeWindow, out int activeProcId);
        return activeProcId == Process.GetCurrentProcess().Id;
    }

    [STAThread]
    public static void Main()
    {
        Application.ThreadException += Application_ThreadException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SealedJournalApp());
    }

    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        MessageBox.Show("An unexpected error occurred. The application will close.", "Error");
        Environment.Exit(1);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBox.Show("A critical error occurred. The application will close.", "Error");
        Environment.Exit(1);
    }
}

public class PasswordDialogResult
{
    public string Password { get; set; }
    public bool Cancelled { get; set; }
}

public static class PasswordDialog
{
    public static PasswordDialogResult Show(string text, string caption)
    {
        Form prompt = new Form
        {
            Width = 500,
            Height = 180,
            Text = caption,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen
        };

        Label textLabel = new Label
        {
            Left = 50,
            Top = 20,
            Text = text,
            Width = 400
        };

        TextBox inputBox = new TextBox
        {
            Left = 50,
            Top = 50,
            Width = 400,
            UseSystemPasswordChar = true
        };

        Button okButton = new Button
        {
            Text = "OK",
            Left = 230,
            Width = 100,
            Top = 90,
            DialogResult = DialogResult.OK
        };

        Button cancelButton = new Button
        {
            Text = "Cancel",
            Left = 340,
            Width = 100,
            Top = 90,
            DialogResult = DialogResult.Cancel
        };

        prompt.Controls.Add(textLabel);
        prompt.Controls.Add(inputBox);
        prompt.Controls.Add(okButton);
        prompt.Controls.Add(cancelButton);

        prompt.AcceptButton = okButton;
        prompt.CancelButton = cancelButton;

        DialogResult result = prompt.ShowDialog();

        return new PasswordDialogResult
        {
            Password = inputBox.Text,
            Cancelled = result == DialogResult.Cancel
        };
    }
}

public class AppConfig
{
    public HotkeyConfig Hotkey { get; set; } = new HotkeyConfig();
    public string InsertDateKey { get; set; } = "F7";
    public string NotesDirectory { get; set; } = "notes";
    public WindowConfig Window { get; set; } = new WindowConfig();
}

public class HotkeyConfig
{
    public string[] Modifiers { get; set; } = new[] { "Ctrl", "Shift" };
    public string Key { get; set; } = "OemMinus";
}

public class WindowConfig
{
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public bool RetainWindowSize { get; set; } = false;
}

public static class SettingsDialog
{
    // Display name to internal key name mapping
    private static readonly (string DisplayName, string InternalName)[] AvailableKeys = new[]
    {
        ("A", "A"), ("B", "B"), ("C", "C"), ("D", "D"), ("E", "E"), ("F", "F"),
        ("G", "G"), ("H", "H"), ("I", "I"), ("J", "J"), ("K", "K"), ("L", "L"),
        ("M", "M"), ("N", "N"), ("O", "O"), ("P", "P"), ("Q", "Q"), ("R", "R"),
        ("S", "S"), ("T", "T"), ("U", "U"), ("V", "V"), ("W", "W"), ("X", "X"),
        ("Y", "Y"), ("Z", "Z"),
        ("F1", "F1"), ("F2", "F2"), ("F3", "F3"), ("F4", "F4"), ("F5", "F5"),
        ("F6", "F6"), ("F7", "F7"), ("F8", "F8"), ("F9", "F9"), ("F10", "F10"),
        ("F11", "F11"), ("F12", "F12"),
        ("0", "0"), ("1", "1"), ("2", "2"), ("3", "3"), ("4", "4"),
        ("5", "5"), ("6", "6"), ("7", "7"), ("8", "8"), ("9", "9"),
        ("NumLock -", "NumPadSubtract"), ("NumLock +", "NumPadAdd"), ("NumLock *", "NumPadMultiply"),
        ("Enter", "Enter"), ("Space", "Space"),
        ("Insert", "Insert"), ("Home", "Home"), ("Del", "Delete"), ("End", "End"),
        ("Pg Up", "PageUp"), ("Pg Dn", "PageDown")
    };

    private static int FindKeyIndexByInternalName(string internalName)
    {
        for (int i = 0; i < AvailableKeys.Length; i++)
        {
            if (AvailableKeys[i].InternalName.Equals(internalName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        
        return 0;
    }

    public static void Show(AppConfig config)
    {
        Form settingsForm = new Form
        {
            Width = 470,
            Height = 480,
            Text = "Settings",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen
        };

        int yPos = 20;
        int labelWidth = 120;
        int controlLeft = 140;
        int controlWidth = 250;
        int rowHeight = 30;

        Label hotkeyLabel = new Label
        {
            Left = 20,
            Top = yPos,
            Width = labelWidth,
            Text = "Hotkey Modifiers:",
            AutoSize = true
        };
        settingsForm.Controls.Add(hotkeyLabel);

        CheckBox ctrlChk = new CheckBox
        {
            Left = controlLeft,
            Top = yPos,
            Text = "Ctrl",
            AutoSize = true,
            Checked = config.Hotkey.Modifiers.Contains("Ctrl", StringComparer.OrdinalIgnoreCase)
        };
        settingsForm.Controls.Add(ctrlChk);

        CheckBox altChk = new CheckBox
        {
            Left = controlLeft + 60,
            Top = yPos,
            Text = "Alt",
            AutoSize = true,
            Checked = config.Hotkey.Modifiers.Contains("Alt", StringComparer.OrdinalIgnoreCase)
        };
        settingsForm.Controls.Add(altChk);

        CheckBox shiftChk = new CheckBox
        {
            Left = controlLeft + 110,
            Top = yPos,
            Text = "Shift",
            AutoSize = true,
            Checked = config.Hotkey.Modifiers.Contains("Shift", StringComparer.OrdinalIgnoreCase)
        };
        settingsForm.Controls.Add(shiftChk);

        CheckBox winChk = new CheckBox
        {
            Left = controlLeft + 170,
            Top = yPos,
            Text = "Win",
            AutoSize = true,
            Checked = config.Hotkey.Modifiers.Contains("Win", StringComparer.OrdinalIgnoreCase)
        };
        settingsForm.Controls.Add(winChk);

        yPos += rowHeight;

        Label keyLabel = new Label
        {
            Left = 20,
            Top = yPos,
            Width = labelWidth,
            Text = "Hotkey Key:"
        };
        settingsForm.Controls.Add(keyLabel);

        ComboBox keyDropdown = new ComboBox
        {
            Left = controlLeft,
            Top = yPos,
            Width = controlWidth,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        
        foreach (var key in AvailableKeys)
        {
            keyDropdown.Items.Add(key.DisplayName);
        }
        keyDropdown.SelectedIndex = FindKeyIndexByInternalName(config.Hotkey.Key);
        settingsForm.Controls.Add(keyDropdown);

        yPos += rowHeight;

        Label insertDateLabel = new Label
        {
            Left = 20,
            Top = yPos,
            Width = labelWidth,
            Text = "Insert Date Key:"
        };
        settingsForm.Controls.Add(insertDateLabel);

        ComboBox insertDateDropdown = new ComboBox
        {
            Left = controlLeft,
            Top = yPos,
            Width = controlWidth,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        
        foreach (var key in AvailableKeys)
        {
            insertDateDropdown.Items.Add(key.DisplayName);
        }
        insertDateDropdown.SelectedIndex = FindKeyIndexByInternalName(config.InsertDateKey);
        settingsForm.Controls.Add(insertDateDropdown);

        yPos += rowHeight;

        Label notesDirLabel = new Label
        {
            Left = 20,
            Top = yPos,
            Width = labelWidth,
            Text = "Notes Directory:"
        };
        settingsForm.Controls.Add(notesDirLabel);

        TextBox notesDirText = new TextBox
        {
            Left = controlLeft,
            Top = yPos,
            Width = controlWidth - 30,
            Text = config.NotesDirectory
        };
        settingsForm.Controls.Add(notesDirText);

        Button browseBtn = new Button
        {
            Left = controlLeft + controlWidth - 25,
            Top = yPos,
            Width = 25,
            Text = "..."
        };
        browseBtn.Click += (s, e) =>
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select notes directory";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    notesDirText.Text = dialog.SelectedPath;
                }
            }
        };
        settingsForm.Controls.Add(browseBtn);

        yPos += rowHeight + 5;

        // Open to Size GroupBox
        GroupBox openToSizeGroup = new GroupBox
        {
            Text = "Open to Size",
            Left = 20,
            Top = yPos,
            Width = 410,
            Height = 95
        };
        settingsForm.Controls.Add(openToSizeGroup);

        int groupYPos = 18;

        Label widthLabel = new Label
        {
            Left = 15,
            Top = groupYPos,
            Width = 50,
            Text = "Width:"
        };
        openToSizeGroup.Controls.Add(widthLabel);

        NumericUpDown widthNum = new NumericUpDown
        {
            Left = 70,
            Top = groupYPos - 2,
            Width = 80,
            Minimum = 400,
            Maximum = 3840,
            Value = config.Window.Width,
            Enabled = !config.Window.RetainWindowSize
        };
        openToSizeGroup.Controls.Add(widthNum);

        Label heightLabel = new Label
        {
            Left = 170,
            Top = groupYPos,
            Width = 50,
            Text = "Height:"
        };
        openToSizeGroup.Controls.Add(heightLabel);

        NumericUpDown heightNum = new NumericUpDown
        {
            Left = 225,
            Top = groupYPos - 2,
            Width = 80,
            Minimum = 300,
            Maximum = 2160,
            Value = config.Window.Height,
            Enabled = !config.Window.RetainWindowSize
        };
        openToSizeGroup.Controls.Add(heightNum);

        groupYPos += 28;

        CheckBox retainSizeChk = new CheckBox
        {
            Left = 15,
            Top = groupYPos + 10,
            Width = 380,
            Height = 20,
            Text = "Retain last window size",
            Checked = config.Window.RetainWindowSize
        };
        openToSizeGroup.Controls.Add(retainSizeChk);

        // Event handler for retain size checkbox
        retainSizeChk.CheckedChanged += (s, e) =>
        {
            widthNum.Enabled = !retainSizeChk.Checked;
            heightNum.Enabled = !retainSizeChk.Checked;
        };

        yPos += openToSizeGroup.Height + 20;

        Button saveButton = new Button
        {
            Text = "Save",
            Left = controlLeft,
            Width = 100,
            Top = yPos,
            DialogResult = DialogResult.OK
        };
        settingsForm.Controls.Add(saveButton);

        Button cancelButton = new Button
        {
            Text = "Cancel",
            Left = controlLeft + 110,
            Width = 100,
            Top = yPos,
            DialogResult = DialogResult.Cancel
        };
        settingsForm.Controls.Add(cancelButton);

        settingsForm.AcceptButton = saveButton;
        settingsForm.CancelButton = cancelButton;

        DialogResult result = settingsForm.ShowDialog();

        if (result == DialogResult.OK)
        {
            var modifiers = new List<string>();
            if (ctrlChk.Checked) modifiers.Add("Ctrl");
            if (altChk.Checked) modifiers.Add("Alt");
            if (shiftChk.Checked) modifiers.Add("Shift");
            if (winChk.Checked) modifiers.Add("Win");

            config.Hotkey.Modifiers = modifiers.ToArray();
            
            int selectedKeyIndex = keyDropdown.SelectedIndex;
            config.Hotkey.Key = selectedKeyIndex >= 0 ? AvailableKeys[selectedKeyIndex].InternalName : "NumPadSubtract";
            
            int selectedDateKeyIndex = insertDateDropdown.SelectedIndex;
            config.InsertDateKey = selectedDateKeyIndex >= 0 ? AvailableKeys[selectedDateKeyIndex].InternalName : "F7";
            
            config.NotesDirectory = notesDirText.Text;
            config.Window.Width = (int)widthNum.Value;
            config.Window.Height = (int)heightNum.Value;
            config.Window.RetainWindowSize = retainSizeChk.Checked;

            SaveConfiguration(config);
        }
    }

    private static void SaveConfiguration(AppConfig config)
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(configPath, json);
    }
}