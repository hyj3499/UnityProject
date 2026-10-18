using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 게임 시작 시 만들어지는 Main Camera의 기본 화면 크기 설정이다.
    /// 씬에 카메라 오브젝트가 없어서 Inspector에서 조절할 수 없는 값을 에셋으로 보관한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Farm MVP/Camera View Settings", fileName = "CameraViewSettings")]
    public class CameraViewSettings : ScriptableObject
    {
        public const string ResourceName = "CameraViewSettings";

        [Min(0.5f)]
        [Tooltip("값이 작을수록 화면의 흰 사각형이 작아지고 더 확대되어 보입니다.")]
        public float orthographicSize = 5.5f;

        [Range(0.6f, 2.5f)]
        [Tooltip("처음 게임을 실행할 때 적용할 줌 배율입니다. 1이 기본 크기입니다.")]
        public float defaultZoom = 1f;

        public static CameraViewSettings Load()
        {
            return Resources.Load<CameraViewSettings>(ResourceName);
        }
    }
}
