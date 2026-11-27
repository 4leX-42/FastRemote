using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FastRemote
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
Admin|Sesión administrativa (/admin)|Administrative session (/admin)
CredNew|Nueva credencial|New credential
CredOwnDetail|Guardada para este servidor|Stored for this server
CredNewDetail|Solo para este servidor|For this server only
CredStoredOn|Guardada en {0}|Stored on {0}
CredUsedOn|Usada en {0} servidores|Used on {0} servers
CredSharedBadge|Compartida|Shared
CredNoneTitle|Sin credencial|No credential
CredNoneDetail|mstsc la pedirá al conectar|mstsc asks for it when connecting
CredKeep|Déjala vacía para conservar la contraseña guardada|Leave it empty to keep the stored password
CredOptional|Si la dejas vacía, mstsc la pedirá al conectar|If left empty, mstsc asks for it when connecting
CredShared|Cambiarla afecta también a {0}|Changing it also affects {0}
CredCopy|Se copia a este servidor. La original y los demás servidores no cambian|It is copied to this server. The original and the other servers do not change
CredRetype|Windows no deja leer esta contraseña: escríbela para cambiar el usuario o el servidor|Windows does not let this password be read: type it to change the user or the server
CredRemoveNote|Se borra la credencial guardada de {0}|The stored credential for {0} is deleted
BOpen|Open|Open
BShow|Show|Show
BRetry|Retry|Retry
BCancel|Cancel|Cancel
BDisconnect|Disconnect|Disconnect
BEdit|Edit|Edit
BConnect|Connect|Connect
BNew|New|New
BOK|OK|OK
BImport|Import|Import
BSave|Save|Save
BClose|Close|Close
BCheck|Check|Check
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
Unknown|Sin comprobar|Not checked
Checking|Comprobando…|Checking…
Stored|Guardada|Stored
NoCred|No guardada|Not stored
Empty|Ninguna conexión coincide|No matching connections
First|Añade tu primera conexión|Add your first connection
Select|Selecciona una conexión|Select a connection
Settings|Ajustes|Settings
Scale|Tamaño de la interfaz|Interface size
Palette|Buscar o ejecutar…|Search or run…
PaletteHint|Conexión, IP, sede, etiqueta o acción|Connection, IP, site, tag or action
PaletteEmpty|Nada coincide con «{0}»|Nothing matches “{0}”
Filter|Filtrar esta lista|Filter this list
Workspace|Espacio de trabajo|Workspace
Infrastructure|Infraestructura|Infrastructure
TagsSection|Etiquetas|Tags
Favorites|Favoritos|Favourites
LiveView|Sesiones activas|Active sessions
Recent|Recientes|Recent
DownView|Sin respuesta|Not responding
ErrorView|Con incidencias|With problems
Credentials|Credenciales|Credentials
Sessions|Sesiones|Sessions
NoSessions|Ninguna sesión abierta|No open sessions
LiveCount|{0} en sesión|{0} in session
DownCount|{0} sin respuesta|{0} not responding
Collapse|Contraer la barra lateral|Collapse the sidebar
Inspector|Mostrar u ocultar los detalles|Show or hide details
Availability|Disponibilidad|Availability
Session|Sesión|Session
Protocol|Protocolo|Protocol
LastUsed|Última conexión|Last connected
Latency|Latencia TCP|TCP latency
LastCheck|Última comprobación|Last check
Kind|Tipo|Type
Tags|Etiquetas|Tags
TagsHint|Separadas por comas|Comma separated
K|Sin tipo|No type
Kserver|Servidor|Server
Kdc|Controlador de dominio|Domain controller
Kbackup|Servidor de copias|Backup server
Kterminal|Servidor de terminales|Terminal server
Kdesktop|Equipo de escritorio|Desktop
Knetwork|Equipo de red|Network device
Up|Puerto RDP accesible|RDP port reachable
Down|Puerto RDP sin respuesta|RDP port not responding
FDns|No se resuelve el nombre (DNS)|The name does not resolve (DNS)
FTimeout|Sin respuesta en {0} s|No answer within {0} s
FRefused|El equipo rechaza el puerto {0}|The host refuses port {0}
FUnreachable|Red o equipo inalcanzable|Network or host unreachable
FOther|Error de red|Network error
ReachNote|Solo indica que el puerto acepta conexiones, no que la credencial sea válida.|It only means the port accepts connections, not that the credential is valid.
SConnecting|Conectando|Connecting
SAuthenticating|Pide credenciales|Asking for credentials
SConnected|Conectada|Connected
SInterrupted|Interrumpida|Interrupted
SEnded|Cerrada|Closed
SInterruptedNote|La conexión se cortó. mstsc enseña el motivo en su ventana.|The connection dropped. mstsc shows the reason in its window.
ClosedEarly|La ventana de mstsc se cerró antes de conectar|The mstsc window closed before connecting
Never|Nunca|Never
Now|ahora|now
MinutesAgo|hace {0} min|{0} min ago
HoursAgo|hace {0} h|{0} h ago
DaysAgo|hace {0} d|{0} d ago
Duration|Duración|Duration
Focus|Ir a la sesión|Go to session
Disconnect|Desconectar|Disconnect
DisconnectNote|Cierra la ventana de mstsc. La sesión sigue abierta en el servidor hasta que cierres sesión dentro de ella.|Closes the mstsc window. The session stays open on the server until you sign out inside it.
DisconnectAll|Desconectar todas|Disconnect all
DisconnectAllConfirm|¿Desconectar {0} sesiones? Seguirán abiertas en los servidores.|Disconnect {0} sessions? They stay open on the servers.
CancelConnect|Cancelar conexión|Cancel connection
Retry|Reintentar|Retry
Another|Abrir otra sesión|Open another session
Check|Comprobar ahora|Check now
CheckVisible|Comprobar las visibles|Check visible ones
AddFavorite|Añadir a favoritos|Add to favourites
RemoveFavorite|Quitar de favoritos|Remove from favourites
SetKind|Tipo de equipo|Host type
Columns|Columnas|Columns
GroupBySite|Agrupar por sede|Group by site
Density|Densidad|Density
compact|Compacta|Compact
comfortable|Cómoda|Comfortable
Selection|{0} seleccionadas|{0} selected
Actions|Acciones|Actions
ConnectionsGroup|Conexiones|Connections
General|General|General
Appearance|Apariencia|Appearance
ConnectionsPage|Conexiones|Connections
Monitoring|Disponibilidad|Availability
SessionsPage|Sesiones|Sessions
Keyboard|Atajos de teclado|Keyboard shortcuts
Security|Seguridad|Security
Data|Datos|Data
About|Acerca de|About
AutoCheck|Volver a comprobar la conexión seleccionada mientras esté seleccionada|Keep checking the selected connection while it is selected
AutoCheckNote|Solo se comprueba la conexión seleccionada, contra su puerto RDP, de una en una. FastRemote no recorre la red por su cuenta. «Comprobar las visibles» (F5) lo pides tú y va de dos en dos.|Only the selected connection is checked, on its RDP port, one at a time. FastRemote never walks the network on its own. “Check visible ones” (F5) is on request and runs two at a time.
CheckEvery|Repetir cada (segundos)|Repeat every (seconds)
Timeout|Tiempo de espera (ms)|Timeout (ms)
ReuseSession|Si ya hay una sesión abierta, Conectar vuelve a su ventana|If a session is already open, Connect returns to its window
AdoptSessions|Al abrir FastRemote, reconocer las ventanas de mstsc ya abiertas|When FastRemote starts, recognise mstsc windows already open
ConfirmDisconnectAll|Pedir confirmación antes de desconectar todas|Ask before disconnecting all
Restore|Restablecer|Restore defaults
SecurityNote|Las contraseñas se guardan en el Administrador de credenciales de Windows (TERMSRV/equipo), cifradas por Windows para tu cuenta. FastRemote no las escribe en sus ficheros ni en los .rdp, no tiene telemetría y no se conecta a internet.|Passwords are stored in Windows Credential Manager (TERMSRV/host), encrypted by Windows for your account. FastRemote never writes them to its files or to .rdp files, has no telemetry and never connects to the internet.
CredentialSummary|{0} de {1} equipos tienen credencial guardada|{0} of {1} hosts have a stored credential
DataNote|Conexiones, ajustes e historial se guardan en JSON en esta carpeta, con una copia .bak de la versión anterior.|Connections, settings and history are stored as JSON in this folder, with a .bak copy of the previous version.
Version|Versión {0}|Version {0}
AboutNote|Lanzador de escritorio remoto para Windows. Licencia MIT. Sin telemetría.|Remote desktop launcher for Windows. MIT licence. No telemetry.
KeyPalette|Buscar o ejecutar|Search or run
KeyFilter|Filtrar la lista|Filter the list
KeyConnect|Conectar o volver a la sesión|Connect or return to the session
KeyConsole|Sesión administrativa (/admin)|Administrative session (/admin)
KeyFull|Conectar a pantalla completa|Connect full screen
KeySwitch|Cambiar de sesión|Switch session
KeyCheck|Comprobar las visibles|Check visible ones
KeyInspector|Mostrar u ocultar los detalles|Show or hide details
KeySidebar|Contraer la barra lateral|Collapse the sidebar
KeyNew|Nueva conexión|New connection
KeyFavorite|Favorito|Favourite
KeyDelete|Eliminar|Delete
Problem|Incidencia|Problem
NoDescription|Sin descripción|No description
GoTo|Ir a|Go to
MenuFile|Archivo|File
MenuView|Ver|View
MenuConnections|Conexiones|Connections
MenuTools|Herramientas|Tools
MenuHelp|Ayuda|Help
Exit|Salir|Exit
Sidebar|Barra lateral|Sidebar
DetailsPanel|Panel de detalles|Details panel
PaletteMenu|Paleta de comandos|Command palette
OpenSession|Abrir sesión|Open session
CheckAll|Comprobar todas|Check all
More|Más opciones|More options
SearchHint|Buscar conexiones (Ctrl+K)|Search connections (Ctrl+K)
FullShort|Pantalla completa|Full screen
ServiceState|Estado del servicio|Service state
Notes|Notas|Notes
UpShort|Disponible|Available
DownShort|Inaccesible|Unreachable
Today|Hoy, {0}|Today, {0}
Yesterday|Ayer, {0}|Yesterday, {0}
SecondsAgo|hace {0} s|{0} s ago
Checked|Comprobado {0}|Checked {0}
LastCheckAll|Última comprobación: {0}|Last check: {0}
CheckingAll|Comprobando {0} de {1}…|Checking {0} of {1}…
ActiveSessions|{0} sesiones activas|{0} active sessions
OneSession|1 sesión activa|1 active session
NoActive|Sin sesiones activas|No active sessions
Available|{0} disponibles|{0} available
Unreachable|{0} inaccesibles|{0} unreachable
Total|{0} conexiones|{0} connections
Address|Dirección IP|IP address
AddressCol|Dirección|Address
TypeCol|Tipo|Type
StateCol|Estado|Status
AllConnections|Todas las conexiones|All connections
SitesHeader|Sedes|Sites
Started|Inicio|Started
ProcessId|Proceso mstsc|mstsc process
AdoptedNote|Ventana de mstsc que ya estaba abierta al iniciar FastRemote: la duración cuenta desde que se detectó.|An mstsc window that was already open when FastRemote started: the duration counts from when it was detected.
Failures|Fallos seguidos|Failures in a row
PortChecked|Puerto comprobado|Port checked
NotesSaved|Notas guardadas|Notes saved
NotesHint|Se guardan en la descripción de la conexión al salir del campo.|Saved to the connection description when you leave the field.
CheckAllOnStart|Comprobar todas las conexiones al iniciar y cada pocos minutos|Check all connections at start and every few minutes
CheckAllMinutes|Repetir la comprobación de todas cada (minutos)|Repeat the check of all every (minutes)
CheckAllNote|Se abre una conexión TCP al puerto RDP de cada servidor guardado, de una en una y espaciadas, sin enviar credenciales. Un antivirus de red puede ver muchas a la vez como un escaneo: por eso van de una en una.|A TCP connection is opened to the RDP port of each saved server, one at a time and spaced out, without sending credentials. A network antivirus may see many at once as a scan: that is why they go one at a time.
SessionNone|Sin sesión abierta|No open session
GoSession|Ir a la sesión|Go to session
ProtocolPort|RDP ({0})|RDP ({0})
StarAdd|Marcar como favorito|Mark as favourite
StarRemove|Quitar de favoritos|Remove from favourites
Show|Mostrar en la lista|Show in the list
Favorite|Favorito|Favourite
Unsure|No se puede saber|Cannot be known
DragHint|Mantén pulsado y arrastra para cambiar el orden|Press, hold and drag to change the order
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
        public static string Ago(DateTime utc)
        {
            var span=DateTime.UtcNow-utc;
            if (span.TotalMinutes<1) return T("Now");
            if (span.TotalHours<1) return T("MinutesAgo",(int)span.TotalMinutes);
            if (span.TotalDays<1) return T("HoursAgo",(int)span.TotalHours);
            if (span.TotalDays<14) return T("DaysAgo",(int)span.TotalDays);
            return utc.ToLocalTime().ToString("d",CultureInfo.CurrentCulture);
        }
        public static string Since(DateTime utc)
        {
            var span=DateTime.UtcNow-utc;
            return span.TotalSeconds<60 ? T("SecondsAgo",Math.Max(1,(int)span.TotalSeconds)) : Ago(utc);
        }
        public static string When(DateTime utc)
        {
            var local=utc.ToLocalTime(); var today=DateTime.Today;
            var time=local.ToString("HH:mm",CultureInfo.CurrentCulture);
            if (local.Date==today) return T("Today",time);
            if (local.Date==today.AddDays(-1)) return T("Yesterday",time);
            return local.ToString("dd/MM/yyyy HH:mm",CultureInfo.CurrentCulture);
        }
        public static string Clock(TimeSpan span) => ((int)span.TotalHours).ToString("00")+":"+span.ToString(@"mm\:ss");
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
        // Semantic tokens. Gold marks identity and focus only; availability and sessions have their own colours.
        static readonly string[] Keys={"Base","Surface","Raised","Hover","Line","Text","Sub","Accent","Up","Down","Live","Checking"};
        static readonly Dictionary<string,string[]> Themes=new Dictionary<string,string[]>{
            ["Carbon"]=new[]{"#111214","#18191C","#222327","#26272C","#303136","#EEEEF0","#9A9DA6","#D3B47D","#4FC78D","#E66B70","#83A8EF","#D9A548"},
            ["Graphite"]=new[]{"#0F1215","#151A1F","#1E242A","#232A31","#2E363F","#ECEFF3","#97A0AB","#8FB4E8","#4FC78D","#E66B70","#B9A2F2","#D9A548"},
            ["Light"]=new[]{"#F4F3F0","#FFFFFF","#FFFFFF","#ECEAE5","#DAD7D0","#1A1A1E","#55565E","#7E5700","#1C7F4E","#B8333A","#2B5BBF","#8F6200"},
            ["Contrast"]=new[]{"#000000","#050505","#121212","#262626","#FFFFFF","#FFFFFF","#EEEEEE","#FFEA5B","#5CFF9D","#FF8A8F","#9CC2FF","#FFD060"}};
        public static IEnumerable<string> ThemeNames => Themes.Keys;
        public static Color Token(string theme,string key) => ColorOf(Themes[Themes.ContainsKey(theme ?? "") ? theme : "Carbon"][Array.IndexOf(Keys,key)]);
        public static void Apply(Settings s)
        {
            var theme=Themes.ContainsKey(s.Theme ?? "") ? s.Theme : "Carbon";
            bool light=theme=="Light";
            var colours=Themes[theme];
            var accent=ColorOf(IsAccent(s.Accent) && !string.IsNullOrEmpty(s.Accent) ? s.Accent : colours[7]);
            var hover=ColorOf(colours[3]);
            while (Ratio(accent,hover)<4.5)
            {
                byte Adjust(byte b) => (byte)(light ? Math.Max(0,b-4) : Math.Min(255,b+4));
                accent=Color.FromRgb(Adjust(accent.R),Adjust(accent.G),Adjust(accent.B));
            }
            for (int i=0;i<Keys.Length;i++) Put(Keys[i],i==7 ? accent : ColorOf(colours[i]));
            Put("Ink",Ratio(accent,Colors.Black)>=Ratio(accent,Colors.White) ? Colors.Black : Colors.White);
            var surface=ColorOf(colours[1]);
            Put("Selected",Mix(surface,accent,light ? .14 : .13));
            Put("AccentSoft",Mix(surface,accent,.22));
            Put("Backdrop",Color.FromArgb(light ? (byte)0x55 : (byte)0x88,0,0,0));
            Words.Set(s.Language);
            foreach(Window w in Application.Current.Windows) if (w.Content is FrameworkElement element) Scale(w,element,s.Scale);
        }
        static Color Mix(Color a,Color b,double t) => Color.FromRgb((byte)(a.R+(b.R-a.R)*t),(byte)(a.G+(b.G-a.G)*t),(byte)(a.B+(b.B-a.B)*t));
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
