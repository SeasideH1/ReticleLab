using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Reticle.Core;

// A separate, message-only window receives documented Windows Raw Input.
// No hooks, game handles, injected code, input suppression or synthesized input.
sealed class RawMouse : IDisposable
{
    readonly object gate = new();
    readonly MouseAccumulator data = new();
    readonly Thread thread;
    readonly ManualResetEventSlim ready = new();
    readonly WindowProc callback;
    IntPtr window, buffer;
    volatile bool stopping;
    public RawMouse() {
        callback=WndProc;thread=new Thread(Run){IsBackground=true,Name="Reticle mouse input"};thread.Start();
        if(!ready.Wait(2000)) { lock(gate) data.State.Error="鼠标接收窗口启动超时"; }
    }
    public MouseState Snapshot() {
        lock(gate) {
            data.State.At=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return JsonSerializer.Deserialize<MouseState>(JsonSerializer.Serialize(data.State))!;
        }
    }
    void Run() {
        string name="Reticle.RawMouse."+Guid.NewGuid().ToString("N");
        IntPtr module=GetModuleHandleW(null);ushort atom=0;
        try {
            var cls=new WindowClass{Procedure=Marshal.GetFunctionPointerForDelegate(callback),Instance=module,ClassName=name};
            atom=RegisterClassW(ref cls);if(atom==0)throw new Win32Exception();
            window=CreateWindowExW(0,name,"",0,0,0,0,0,new IntPtr(-3),IntPtr.Zero,module,IntPtr.Zero);
            if(window==IntPtr.Zero)throw new Win32Exception();
            buffer=Marshal.AllocHGlobal(1024);
            var device=new Device{Page=1,Usage=2,Flags=0x100,Target=window}; // RIDEV_INPUTSINK, mouse only
            if(!RegisterRawInputDevices(new[]{device},1,(uint)Marshal.SizeOf<Device>()))throw new Win32Exception();
            lock(gate) data.State.Enabled=true;
            ready.Set();
            if(stopping)return;
            int result;
            while((result=GetMessageW(out var message,IntPtr.Zero,0,0))>0) {TranslateMessage(ref message);DispatchMessageW(ref message);}
            if(result<0)throw new Win32Exception();
        } catch(Exception e) {lock(gate)data.State.Error=e.Message;}
        finally {
            lock(gate)data.State.Enabled=false;
            ready.Set();
            if(window!=IntPtr.Zero) {
                var remove=new Device{Page=1,Usage=2,Flags=1,Target=IntPtr.Zero};
                RegisterRawInputDevices(new[]{remove},1,(uint)Marshal.SizeOf<Device>());
                DestroyWindow(window);window=IntPtr.Zero;
            }
            if(buffer!=IntPtr.Zero){Marshal.FreeHGlobal(buffer);buffer=IntPtr.Zero;}
            if(atom!=0)UnregisterClassW(name,module);
        }
    }
    IntPtr WndProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam) {
        try {
            if(message==0xFF && buffer!=IntPtr.Zero) {
                uint size=1024,header=(uint)(8+2*IntPtr.Size);
                uint received=GetRawInputData(lParam,0x10000003,buffer,ref size,header);
                if(received!=uint.MaxValue && received>=header+24 && Marshal.ReadInt32(buffer)==0) {
                    int start=(int)header;
                    lock(gate)data.Apply(Marshal.ReadInt32(buffer,start+12),Marshal.ReadInt32(buffer,start+16),
                        (ushort)Marshal.ReadInt16(buffer,start),(ushort)Marshal.ReadInt16(buffer,start+4),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }
            }
            if(message==0x10) {DestroyWindow(hwnd);return IntPtr.Zero;}
            if(message==2) {PostQuitMessage(0);return IntPtr.Zero;}
        } catch(Exception e) {lock(gate)data.State.Error=e.Message;}
        return DefWindowProcW(hwnd,message,wParam,lParam);
    }
    public void Dispose() {
        stopping=true;
        if(window!=IntPtr.Zero)PostMessageW(window,0x10,IntPtr.Zero,IntPtr.Zero);
        thread.Join(2000);
        // The worker owns the HWND, registration and buffer, including shutdown cleanup.
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate IntPtr WindowProc(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct Device {public ushort Page,Usage;public uint Flags;public IntPtr Target;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct WindowClass {
        public uint Style;public IntPtr Procedure;public int ClassExtra,WindowExtra;public IntPtr Instance,Icon,Cursor,Background;
        public string? MenuName;public string ClassName;
    }
    [StructLayout(LayoutKind.Sequential)] struct Message {public IntPtr Window;public uint Id;public UIntPtr W;public IntPtr L;public uint Time;public int X,Y;public uint Private;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)] static extern ushort RegisterClassW(ref WindowClass cls);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern bool UnregisterClassW(string name,IntPtr instance);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)] static extern IntPtr CreateWindowExW(uint ex,string cls,string title,uint style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr parameter);
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterRawInputDevices(Device[] devices,uint count,uint size);
    [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint header);
    [DllImport("user32.dll",ExactSpelling=true)] static extern int GetMessageW(out Message message,IntPtr window,uint min,uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll",ExactSpelling=true)] static extern IntPtr DispatchMessageW(ref Message message);
    [DllImport("user32.dll",ExactSpelling=true)] static extern IntPtr DefWindowProcW(IntPtr window,uint msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll",ExactSpelling=true)] static extern bool PostMessageW(IntPtr window,uint msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int code);
}
