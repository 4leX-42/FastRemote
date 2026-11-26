using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quaygate
{
    static class Words
    {
        // Stable keys are also used by compiled XAML through dynamic resources.
        const string Table=@"Search|Buscar conexiones|Search connections
Sites|Sedes|Sites
ListTitle|Conexiones|Connections
All|Todas|All
NoSite|Sin sede|Unassigned
New|Nueva conexión|New connection
Edit|Editar|Edit
Delete|Eliminar|Delete
Duplicate|Duplicar|Duplicate
Copy|Copiar equipo|Copy host
Connect|Conectar|Connect
Console|Consola|Console
Full|Pantalla completa|Full screen
Cred|Credencial|Credential
RemoveCred|Quitar credencial|Remove credential
Bulk|Credencial para varias|Bulk credential
Import|Importar de mRemoteNG|Import from mRemoteNG
ImportPath|Archivo de conexiones de mRemoteNG|mRemoteNG connections file
ImportHelp|Se importarán los perfiles de escritorio remoto y sus opciones. Las conexiones existentes conservarán sus nombres y sedes.|Remote desktop profiles and their settings will be imported. Existing connections will keep their names and sites.
Host|Equipo|Host
User|Usuario|User
Domain|Dominio|Domain
Port|Puerto|Port
Site|Sede|Site
Name|Nombre|Name
Description|Descripción|Description
Details|Detalles|Details
Unknown|Sin comprobar|Unchecked
Checking|Comprobando…|Checking…
Online|Puerto accesible|Port reachable
Offline|Sin respuesta|No response
Stored|Guardada|Stored
NoCred|Sin credencial guardada|No stored credential
Empty|Ninguna conexión coincide|No matching connections
First|Añade tu primera conexión|Add your first connection
Select|Selecciona una conexión|Select a connection
Settings|Ajustes|Settings
Scale|Tamaño de la interfaz|Interface size
Theme|Tema|Theme
Carbon|Carbón y oro|Carbon gold
Graphite|Grafito|Graphite
Light|Claro|Light
Contrast|Alto contraste|High contrast
Accent|Color de acento (#RRGGBB)|Accent colour (#RRGGBB)
Language|Idioma|Language
auto|Automático|Automatic
es|Español|Español
en|English|English
Order|Orden de las sedes|Site order
Up|Subir|Move up
Down|Bajar|Move down
Rename|Renombrar sede|Rename site
SiteOptions|Opciones de la sede|Site options
Folder|Abrir carpeta de datos|Open data folder
OK|Aceptar|OK
Cancel|Cancelar|Cancel
Close|Cerrar|Close
Minimize|Minimizar|Minimize
Maximize|Maximizar o restaurar|Maximize or restore
DeleteConfirm|¿Eliminar «{0}» de la lista?|Remove “{0}” from the list?
RemoveConfirm|¿Quitar la credencial guardada para {0}?|Remove the stored credential for {0}?
Advanced|Avanzado|Advanced
Inherit|Heredar|Inherit
Default|Predeterminado|Default
Yes|Sí|Yes
No|No|No
Invalid|Revisa el valor de {0}.|Check the value of {0}.
NeedHost|Falta el equipo o su nombre no es válido.|Enter a valid host name.
NeedPassword|Falta la contraseña.|Enter a password.
NeedUser|Indica un usuario para cada equipo seleccionado.|Enter a user name for each selected host.
Password|Contraseña|Password
OwnUser|Deja el usuario vacío para usar el de cada conexión.|Leave the user blank to use each connection’s own account.
Same|Mismo usuario|Same user
Count|{0} conexiones|{0} connections
Filtered|{0} de {1} conexiones|{0} of {1} connections
Done|Guardado|Saved
Imported|{0} perfiles leídos|Read {0} profiles
CredentialCount|Credenciales guardadas: {0}|Credentials saved: {0}
Partial|Se guardaron {0} credenciales antes del error.|Saved {0} credentials before the error.
Failure|No se pudo completar la operación. Consulta error.log en la carpeta de datos.|The operation could not be completed. See error.log in the data folder.
Resolution|Resolución|Resolution
fit|Ajustar a la ventana|Fit to window
full|Pantalla completa|Full screen
smart|Escalado inteligente|Smart sizing
fixed|Tamaño fijo|Fixed size
Width|Ancho en píxeles|Width in pixels
Height|Alto en píxeles|Height in pixels
Colors|Profundidad de color (bits)|Colour depth (bits)
MultiMonitor|Usar varios monitores|Use multiple monitors
RedirectDiskDrives|Redirigir unidades|Redirect drives
RedirectPrinters|Redirigir impresoras|Redirect printers
RedirectPorts|Redirigir puertos serie|Redirect serial ports
RedirectSmartCards|Redirigir tarjetas inteligentes|Redirect smart cards
RedirectClipboard|Redirigir portapapeles|Redirect clipboard
RedirectSound|Reproducción de audio|Audio playback
RedirectSound0|En este equipo|On this computer
RedirectSound1|En el equipo remoto|On the remote computer
RedirectSound2|Desactivada|Off
RedirectAudioCapture|Redirigir micrófono|Redirect microphone
RedirectKeys|Combinaciones de teclas de Windows|Windows key combinations
RedirectKeys0|En este equipo|On this computer
RedirectKeys1|En el equipo remoto|On the remote computer
RedirectKeys2|Solo a pantalla completa|Only in full screen
DisplayWallpaper|Fondo de escritorio|Desktop background
DisplayThemes|Temas de escritorio|Desktop themes
EnableFontSmoothing|Suavizado de fuentes|Font smoothing
EnableDesktopComposition|Composición del escritorio|Desktop composition
CacheBitmaps|Caché de mapas de bits|Bitmap cache
ConnectToConsole|Sesión de administración|Administrative session
UseCredSsp|Autenticación de nivel de red (CredSSP)|Network level authentication (CredSSP)
RDPAuthenticationLevel|Autenticación del servidor|Server authentication
RDPAuthenticationLevel0|Conectar siempre|Always connect
RDPAuthenticationLevel1|Exigir autenticación|Require authentication
RDPAuthenticationLevel2|Avisar si falla|Warn on failure
RDGatewayUsageMethod|Uso de la puerta de enlace|Gateway usage
RDGatewayUsageMethod0|Nunca|Never
RDGatewayUsageMethod1|Siempre|Always
RDGatewayUsageMethod2|Detectar|Detect
RDGatewayHostname|Servidor de puerta de enlace|Gateway host
RDGatewayUseConnectionCredentials|Usar la misma credencial en la puerta de enlace|Use the same credential for the gateway
LoadBalanceInfo|Información de equilibrio de carga|Load balancing information";
        static readonly Dictionary<string,string[]> strings=Table.Split('\n').Select(s=>s.TrimEnd('\r').Split('|')).ToDictionary(a=>a[0]);
        static bool spanish;
        public static string T(string key,params object[] args)
        {
            var text=strings.TryGetValue(key,out var values) ? values[spanish ? 1 : 2] : key;
            return args.Length==0 ? text : string.Format(CultureInfo.CurrentCulture,text,args);
        }
        public static void Set(string language)
        {
            spanish=language=="es" || (language!="en" && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="es");
            foreach(var key in strings.Keys) Application.Current.Resources["L"+key]=T(key);
        }
    }

    static class Appearance
    {
        public static readonly FontFamily Font=new FontFamily("Segoe UI Variable Text, Segoe UI");
        static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        static double Linear(byte b) { double s=b/255d; return s<=.04045 ? s/12.92 : Math.Pow((s+.055)/1.055,2.4); }
        static double Luma(Color c) => .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);
        public static double Ratio(Color a,Color b)
        {
            double x=Luma(a),y=Luma(b);
            return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
        }
        public static bool IsAccent(string hex) => string.IsNullOrEmpty(hex) || System.Text.RegularExpressions.Regex.IsMatch(hex,@"\A#[a-fA-F0-9]{6}\z");
        public static void Apply(Settings s)
        {
            bool light=s.Theme=="Light", high=s.Theme=="Contrast", graphite=s.Theme=="Graphite";
            string[] colours=light ? new[]{"#F4F2EF","#FFFFFF","#E6E3DE","#B4B0AA","#1B1B20","#4B4B53","#755000"} :
                high ? new[]{"#000000","#090909","#202020","#FFFFFF","#FFFFFF","#EEEEEE","#FFEA5B"} :
                new[]{"#161618","#1D1D21","#2C2C32","#52525C","#F0F0F2","#C1C1C9",graphite ? "#A5C6EC" : "#D4B37A"};
            string[] keys={"Base","Surface","Hover","Line","Text","Sub","Accent"};
            var accent=ColorOf(IsAccent(s.Accent) && !string.IsNullOrEmpty(s.Accent) ? s.Accent : colours[6]);
            var hover=ColorOf(colours[2]);
            while (Ratio(accent,hover)<4.5)
            {
                byte Adjust(byte b) => (byte)(light ? Math.Max(0,b-4) : Math.Min(255,b+4));
                accent=Color.FromRgb(Adjust(accent.R),Adjust(accent.G),Adjust(accent.B));
            }
            for (int i=0;i<keys.Length;i++) Put(keys[i],i==6 ? accent : ColorOf(colours[i]));
            Put("Ink",Ratio(accent,Colors.Black)>=Ratio(accent,Colors.White) ? Colors.Black : Colors.White);
            Words.Set(s.Language);
            foreach(Window w in Application.Current.Windows) if (w.Content is FrameworkElement element) Scale(w,element,s.Scale);
        }
        static void Put(string key,Color color)
        {
            var brush=new SolidColorBrush(color); brush.Freeze();
            Application.Current.Resources[key]=brush;
        }
        public static void Scale(Window w,FrameworkElement root,double scale)
        {
            w.FontFamily=Font; w.FontSize=13; w.UseLayoutRounding=true;
            w.SetResourceReference(Control.ForegroundProperty,"Text");
            w.SetResourceReference(Control.BackgroundProperty,"Base");
            TextOptions.SetTextFormattingMode(w,scale==1 ? TextFormattingMode.Display : TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(w,TextRenderingMode.ClearType);
            root.LayoutTransform=new ScaleTransform(scale,scale);
        }
    }
}
