using Foundation;

namespace Breb.App;

// Mac Catalyst (macOS 27) es una app UIKit y tiene el mismo requisito de
// ciclo de vida basado en UIScene que iOS 27. Sin un SceneDelegate registrado
// que coincida con UISceneDelegateClassName del Info.plist, la app arranca
// pero no se crea la ventana de MAUI: queda la pantalla gris.
// MauiUISceneDelegate crea la ventana en WillConnect y reenvia los callbacks
// de escena al ciclo de vida de Window. La clase puede quedar vacia: NO
// agregues reenvios propios o cada evento llegaria dos veces.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
