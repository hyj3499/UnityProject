using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 한 번 툭 건드렸을 때 잠깐 흔들리는 반응. 나무를 베거나 작물 위를 지나갈 때처럼
    /// <b>짧게 일어났다 원래대로 돌아오는</b> 움직임 전용이다.
    ///
    /// 물결처럼 계속 도는 것(SeasonalTile의 프레임 애니메이션)과는 다른 물건이다:
    ///  - 계속 도는 것은 <b>데이터</b>다. 그림 여러 장을 타일에 넣어 두면 Tilemap이 알아서 돌린다.
    ///  - 이건 <b>사건</b>이다. 무슨 일이 있었을 때만 잠깐 돌고 스스로 꺼진다.
    /// 같은 파일에 섞으면 둘 다 어정쩡해지므로 나눠 두었다.
    ///
    /// 밑동을 축으로 돌린다 — 나무를 한가운데를 축으로 돌리면 뿌리가 땅에서 떠 보인다.
    /// </summary>
    public class Wobble : MonoBehaviour
    {
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _pivotOffset;   // 밑동이 중심에서 얼마나 아래인지
        private float _degrees, _duration, _cycles, _elapsed;
        private bool _running;

        /// <summary>
        /// 이 오브젝트를 잠깐 흔든다. 이미 흔들리는 중이면 처음부터 다시 흔든다.
        /// </summary>
        /// <param name="degrees">최대 기울기(도).</param>
        /// <param name="duration">전체 시간(초). 끝으로 갈수록 잦아든다.</param>
        /// <param name="cycles">그 동안 좌우로 몇 번 오갈지.</param>
        public static void Play(Component target, float degrees = 7f, float duration = 0.35f, float cycles = 3f)
        {
            if (target == null) return;

            var w = target.GetComponent<Wobble>();
            if (w == null) w = target.gameObject.AddComponent<Wobble>();
            w.Begin(degrees, duration, cycles);
        }

        private void Begin(float degrees, float duration, float cycles)
        {
            // 흔들리는 도중에 또 맞으면 기울어진 상태를 기준으로 삼지 않도록, 처음 한 번만 기억한다.
            if (!_running)
            {
                _basePosition = transform.localPosition;
                _baseRotation = transform.localRotation;
                _pivotOffset = FootOffset();
            }

            _degrees = degrees;
            _duration = Mathf.Max(0.01f, duration);
            _cycles = cycles;
            _elapsed = 0f;
            _running = true;
            enabled = true;
        }

        /// <summary>스프라이트의 아래쪽 끝 = 밑동. 스프라이트가 없으면 그냥 제자리에서 돈다.</summary>
        private Vector3 FootOffset()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return Vector3.zero;
            return new Vector3(0f, sr.sprite.bounds.min.y, 0f);
        }

        private void LateUpdate()
        {
            if (!_running) return;

            _elapsed += Time.deltaTime;
            float t = _elapsed / _duration;

            // 원래 자세로 되돌린 뒤 이번 프레임의 각도만큼 다시 기울인다.
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;

            if (t >= 1f)
            {
                _running = false;
                enabled = false;
                return;
            }

            float damp = 1f - t;                                   // 끝으로 갈수록 잦아든다
            float angle = Mathf.Sin(t * _cycles * Mathf.PI * 2f) * _degrees * damp;
            transform.RotateAround(transform.position + _pivotOffset, Vector3.forward, angle);
        }
    }
}
