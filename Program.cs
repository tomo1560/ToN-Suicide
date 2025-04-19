using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

public class MainForm : Form
{
    private const int LabelWidth = 100;
    private const int TextBoxWidth = 200;
    private const int ControlHeight = 25;
    private const int ControlMargin = 20;

    private TextBox txtPort;
    private TextBox txtDragTime;
    private TextBox txtWindowName;
    private Button btnToggle;
    private Label lblStatus;
    private CheckBox chkAutoStart;
    private OscListener oscListener;

    private readonly string configFilePath = "config.conf";

    public MainForm()
    {
        InitializeForm();
        InitializeControls();
        LoadConfig();

        oscListener = new OscListener(UpdateStatus, ProcessOscMessage);

        if (chkAutoStart.Checked)
        {
            BtnToggle_Click(null, EventArgs.Empty);
        }
    }

    private void InitializeForm()
    {
        this.Text = "ToN Suicide";
        this.Size = new System.Drawing.Size(350, 400);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
    }

    private void InitializeControls()
    {
        int currentY = ControlMargin;

        Label lblPort = CreateLabel("Port:", ControlMargin, currentY);
        txtPort = CreateTextBox(ControlMargin + LabelWidth, currentY);
        currentY += ControlHeight + ControlMargin;

        Label lblDragTime = CreateLabel("Drag Time (ms):", ControlMargin, currentY);
        txtDragTime = CreateTextBox(ControlMargin + LabelWidth, currentY);
        currentY += ControlHeight + ControlMargin;

        Label lblWindowName = CreateLabel("Window Name:", ControlMargin, currentY);
        txtWindowName = CreateTextBox(ControlMargin + LabelWidth, currentY);
        currentY += ControlHeight + ControlMargin;

        chkAutoStart = new CheckBox()
        {
            Text = "Auto Start",
            Location = new System.Drawing.Point(ControlMargin, currentY),
            AutoSize = true
        };
        currentY += ControlHeight + ControlMargin;

        btnToggle = new Button()
        {
            Text = "Start",
            Location = new System.Drawing.Point((this.ClientSize.Width - TextBoxWidth) / 2, currentY),
            Width = TextBoxWidth,
            Height = ControlHeight
        };
        btnToggle.Click += BtnToggle_Click;
        currentY += ControlHeight + ControlMargin;

        lblStatus = new Label()
        {
            Text = "Status: Stopped",
            Location = new System.Drawing.Point(ControlMargin, currentY),
            AutoSize = true,
            ForeColor = System.Drawing.Color.Black
        };

        this.Controls.Add(lblPort);
        this.Controls.Add(txtPort);
        this.Controls.Add(lblDragTime);
        this.Controls.Add(txtDragTime);
        this.Controls.Add(lblWindowName);
        this.Controls.Add(txtWindowName);
        this.Controls.Add(chkAutoStart);
        this.Controls.Add(btnToggle);
        this.Controls.Add(lblStatus);
    }

    private Label CreateLabel(string text, int x, int y)
    {
        return new Label()
        {
            Text = text,
            Location = new System.Drawing.Point(x, y),
            Width = LabelWidth,
            Height = ControlHeight
        };
    }

    private TextBox CreateTextBox(int x, int y)
    {
        return new TextBox()
        {
            Location = new System.Drawing.Point(x, y),
            Width = TextBoxWidth,
            Height = ControlHeight
        };
    }

    private void LoadConfig()
    {
        if (File.Exists(configFilePath))
        {
            var lines = File.ReadAllLines(configFilePath);
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (key == "Port")
                        txtPort.Text = value;
                    else if (key == "DragTime")
                        txtDragTime.Text = value;
                    else if (key == "WindowName")
                        txtWindowName.Text = value;
                    else if (key == "AutoStart")
                        chkAutoStart.Checked = bool.Parse(value);
                }
            }
        }
        else
        {
            txtPort.Text = "9001";
            txtDragTime.Text = "5000";
            txtWindowName.Text = "VRChat";
            chkAutoStart.Checked = false;
        }
    }

    private void SaveConfig()
    {
        var lines = new[]
        {
            $"Port={txtPort.Text}",
            $"DragTime={txtDragTime.Text}",
            $"WindowName={txtWindowName.Text}",
            $"AutoStart={chkAutoStart.Checked}"
        };
        File.WriteAllLines(configFilePath, lines);
    }

    private void BtnToggle_Click(object sender, EventArgs e)
    {
        if (!oscListener.IsRunning)
        {
            try
            {
                int port = int.Parse(txtPort.Text);
                SaveConfig();

                oscListener.Start(port);

                UpdateStatus("Status: Running", System.Drawing.Color.Green);
                btnToggle.Text = "Stop";
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", System.Drawing.Color.Red);
            }
        }
        else
        {
            oscListener.Stop();

            UpdateStatus("Status: Stopped", System.Drawing.Color.Black);
            btnToggle.Text = "Start";
        }
    }

    private void UpdateStatus(string message, System.Drawing.Color color)
    {
        lblStatus.Text = message;
        lblStatus.ForeColor = color;
    }

    private void ProcessOscMessage(byte[] data)
    {
        Task.Run(() => ParseOscMessage(data));
    }

    private void ParseOscMessage(byte[] data)
    {
        try
        {
            int index = 0;

            string address = ReadOscString(data, ref index);
            Console.WriteLine($"[OSC LOG] Address: {address}");

            string typeTag = ReadOscString(data, ref index);
            Console.WriteLine($"[OSC LOG] TypeTag: {typeTag}");

            if (address == "/avatar/parameters/ton_suicide" && typeTag == ",T")
            {
                Console.WriteLine($"[OSC LOG] Triggering PerformDrag for address: {address}");
                PerformDrag(txtWindowName.Text, int.Parse(txtDragTime.Text));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to parse OSC message: {ex.Message}");
        }
    }

    private string ReadOscString(byte[] data, ref int index)
    {
        int startIndex = index;
        while (data[index] != 0) index++;
        string result = Encoding.UTF8.GetString(data, startIndex, index - startIndex);
        index = (index + 4) & ~3;
        return result;
    }

    private int ReadOscInt(byte[] data, ref int index)
    {
        int value = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, index));
        index += 4;
        return value;
    }

    private float ReadOscFloat(byte[] data, ref int index)
    {
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(data, index, 4);
        }
        float value = BitConverter.ToSingle(data, index);
        index += 4;
        return value;
    }

    private void PerformDrag(string windowName, int dragTime)
    {
        IntPtr hWnd = FindWindow(null, windowName);
        if (hWnd == IntPtr.Zero)
        {
            Invoke(new Action(() =>
            {
                lblStatus.Text = "Error: Window Not Found";
                lblStatus.ForeColor = System.Drawing.Color.Red;
            }));
            return;
        }

        if (!GetWindowRect(hWnd, out RECT rect))
        {
            Invoke(new Action(() =>
            {
                lblStatus.Text = "Error: Unable to Get Window Position";
                lblStatus.ForeColor = System.Drawing.Color.Red;
            }));
            return;
        }

        int centerX = (rect.Left + rect.Right) / 2;
        int centerY = rect.Top + 10;

        SetCursorPos(centerX, centerY);
        mouse_event(MOUSEEVENTF_LEFTDOWN, centerX, centerY, 0, UIntPtr.Zero);

        Thread.Sleep(dragTime);

        mouse_event(MOUSEEVENTF_LEFTUP, centerX, centerY, 0, UIntPtr.Zero);
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

public class OscListener
{
    private UdpClient udpClient;
    private CancellationTokenSource cts;
    private readonly Action<string, System.Drawing.Color> updateStatus;
    private readonly Action<byte[]> processMessage;

    public bool IsRunning { get; private set; }

    public OscListener(Action<string, System.Drawing.Color> updateStatus, Action<byte[]> processMessage)
    {
        this.updateStatus = updateStatus;
        this.processMessage = processMessage;
    }

    public void Start(int port)
    {
        if (IsRunning) return;

        cts = new CancellationTokenSource();
        IsRunning = true;

        Task.Run(async () =>
        {
            try
            {
                udpClient = new UdpClient(port);
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync(cts.Token);
                        processMessage(result.Buffer);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                updateStatus?.Invoke($"Error: {ex.Message}", System.Drawing.Color.Red);
            }
            finally
            {
                IsRunning = false;
                udpClient?.Close();
            }
        }, cts.Token);
    }

    public void Stop()
    {
        if (!IsRunning) return;

        cts.Cancel();
        IsRunning = false;

        try
        {
            udpClient?.Close();
            udpClient?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[ERROR] SocketException during Stop: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[ERROR] IOException during Stop: {ex.Message}");
        }
    }
}