using Foundation;

namespace Breb.App;

// iOS 27 / Xcode 27 exige el ciclo de vida basado en UIScene: la app ya no
// puede dejar que el AppDelegate sea dueno de la unica ventana. MauiUISceneDelegate
// (parte de Microsoft.Maui desde 2021) crea la ventana de MAUI en WillConnect y
// reenvia los callbacks de escena al ciclo de vida de Window. La clase puede
// quedar vacia: NO agregues reenvios propios o cada evento llegaria dos veces.
// El nombre registrado "SceneDelegate" debe coincidir con UISceneDelegateClassName
// en Info.plist.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
