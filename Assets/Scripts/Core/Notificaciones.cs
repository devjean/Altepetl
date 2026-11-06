using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace Altepetl
{
    /// <summary>
    /// Avisos en el celular sin servidor (paquete Mobile Notifications): el juego los programa al cerrarse o
    /// minimizarse, con la hora exacta en que ocurrirá algo que ya se decidió (un ataque, el fin del escudo),
    /// y los cancela todos al volver. En el editor y en otras plataformas no hacen nada.
    /// </summary>
    public static class Notificaciones
    {
        private const string Canal = "aldea";
        private static bool _lista;

        public static bool Disponibles =>
#if UNITY_ANDROID || UNITY_IOS
            !Application.isEditor;
#else
            false;
#endif

        private static void Preparar()
        {
            if (_lista) return;
            _lista = true;
#if UNITY_ANDROID
            AndroidNotificationCenter.Initialize();
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(
                Canal, "Aldea", "Ataques y escudo de tu aldea", Importance.Default));
#endif
        }

        /// <summary>
        /// Pide permiso al sistema (Android 13 e iOS lo exigen; antes no se muestra nada). Llamar con
        /// StartCoroutine; termina cuando el jugador contesta. Si lo niega, los avisos se programan en silencio.
        /// </summary>
        public static IEnumerator PedirPermiso()
        {
            if (!Disponibles) yield break;
#if UNITY_ANDROID
            Preparar();
            if (AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.NotRequested)
            {
                var peticion = new PermissionRequest();
                while (peticion.Status == PermissionStatus.RequestPending) yield return null;
            }
#elif UNITY_IOS
            using (var peticion = new AuthorizationRequest(
                AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false))
            {
                while (!peticion.IsFinished) yield return null;
            }
#endif
        }

        /// <summary>Programa un aviso para dentro de tantos segundos.</summary>
        public static void Programar(string titulo, string texto, double segundos)
        {
            if (!Disponibles) return;
            if (segundos < 1) segundos = 1;
#if UNITY_ANDROID
            Preparar();
            var aviso = new AndroidNotification(titulo, texto, System.DateTime.Now.AddSeconds(segundos));
            AndroidNotificationCenter.SendNotification(aviso, Canal);
#elif UNITY_IOS
            var aviso = new iOSNotification
            {
                Title = titulo,
                Body = texto,
                Badge = 1,
                ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = System.TimeSpan.FromSeconds(segundos),
                    Repeats = false,
                },
            };
            iOSNotificationCenter.ScheduleNotification(aviso);
#endif
        }

        /// <summary>Al volver al juego: nada de lo programado sigue valiendo, y se limpia lo que ya se mostró.</summary>
        public static void CancelarTodas()
        {
            if (!Disponibles) return;
#if UNITY_ANDROID
            Preparar();
            AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
#endif
        }
    }
}
