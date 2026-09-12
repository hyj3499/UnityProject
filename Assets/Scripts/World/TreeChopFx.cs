using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 나무를 벨 때 보이는 것들. <b>사건</b>에만 잠깐 돌고 스스로 사라지는 연출이라
    /// Wobble과 같은 자리에 둔다 (계속 도는 애니메이션은 데이터로 처리한다 — Wobble 주석 참고).
    ///
    ///  · 한 번 칠 때마다  : 나뭇잎 네 장이 가지에서 떨어져 나온다
    ///  · 쓰러뜨렸을 때    : 레벨3 윗부분이 옆으로 기울며 넘어가고, 밑에 있던 그루터기가 드러난다
    ///
    /// 둘 다 <b>이미 만들어진 스프라이트만 옮긴다</b> — 나무 데이터(TreeFeature)는 치는 순간
    /// 이미 갱신돼 있고, 연출은 그 뒤를 따라가기만 하므로 중간에 끊겨도 게임 상태는 멀쩡하다.
    /// </summary>
    public static class TreeChopFx
    {
        /// <summary>나뭇잎이 다 떨어지기까지 걸리는 시간(초).</summary>
        private const float LeafFallTime = 1.1f;
        /// <summary>윗부분이 넘어가는 데 걸리는 시간(초).</summary>
        private const float TopFallTime = 0.55f;

        /// <summary>
        /// 나뭇잎을 흩뿌린다. 잎은 나무 그림 <b>위쪽(가지가 있는 높이)</b>에서 나와 떨어지므로,
        /// 밑동 위치와 잎이 달려 있는 높이를 받는다.
        /// </summary>
        public static void ScatterLeaves(Transform parent, Sprite[] leaves, Vector2 foot,
                                         float canopyHeight, int sortingOrder)
        {
            if (parent == null || leaves == null || leaves.Length == 0) return;

            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i] == null) continue;

                var go = new GameObject("leaf");
                go.transform.SetParent(parent, false);
                // 가지 전체에 골고루 퍼지도록 잎마다 조금씩 다른 자리에서 시작한다.
                float side = Random.Range(-0.9f, 0.9f);
                float top = canopyHeight + Random.Range(-0.5f, 0.5f);
                go.transform.position = new Vector3(foot.x + side, foot.y + top, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = leaves[i];
                sr.sortingOrder = sortingOrder;

                var leaf = go.AddComponent<FallingLeaf>();
                leaf.Begin(top + 0.35f, LeafFallTime + Random.Range(-0.15f, 0.2f), i);
            }
        }

        /// <summary>
        /// 다 자란 나무의 윗부분을 옆으로 넘어뜨린다. 그림의 기준점이 밑동이라 그 자리를 축으로
        /// 돌리기만 하면 되고, 밑에 있던 그루터기는 손대지 않아 그대로 남는다.
        /// </summary>
        /// <param name="dir">+1이면 오른쪽, -1이면 왼쪽으로 넘어간다.</param>
        public static void FellTop(SpriteRenderer top, float dir)
        {
            if (top == null) return;
            top.gameObject.AddComponent<FallingTreeTop>().Begin(dir, TopFallTime);
        }
    }

    /// <summary>떨어지는 나뭇잎 한 장. 좌우로 하늘거리며 내려앉고 마지막에 옅어진다.</summary>
    public class FallingLeaf : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Vector3 _start;
        private float _drop, _duration, _elapsed;
        private float _swayPhase, _swayWidth, _spin, _drift;

        internal void Begin(float drop, float duration, int index)
        {
            _sr = GetComponent<SpriteRenderer>();
            _start = transform.position;
            _drop = Mathf.Max(0.1f, drop);
            _duration = Mathf.Max(0.1f, duration);
            _elapsed = 0f;

            // 잎마다 다른 흔들림 — 네 장이 한 덩어리로 움직이면 이펙트로 보이지 않는다.
            _swayPhase = index * Mathf.PI * 0.5f + Random.Range(0f, Mathf.PI);
            _swayWidth = Random.Range(0.18f, 0.38f);
            _spin = Random.Range(-160f, 160f);
            _drift = Random.Range(-0.35f, 0.35f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // 아래로는 일정하게, 좌우로는 하늘거리며.
            float sway = Mathf.Sin(_swayPhase + t * Mathf.PI * 3.2f) * _swayWidth;
            transform.position = _start + new Vector3(sway + _drift * t, -_drop * t, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, _spin * t);

            // 바닥에 닿을 즈음에만 옅어진다 — 처음부터 흐려지면 무엇이 떨어지는지 안 보인다.
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = t < 0.7f ? 1f : Mathf.InverseLerp(1f, 0.7f, t);
                _sr.color = c;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// 쓰러지는 나무 윗부분. 밑동(스프라이트 기준점)을 축으로 점점 빠르게 기울다가,
    /// 다 넘어가면 옅어지며 사라진다 — 그 자리에는 그루터기가 남아 있다.
    /// </summary>
    public class FallingTreeTop : MonoBehaviour
    {
        private const float FallAngle = 82f;
        private const float FadeTime = 0.3f;

        private SpriteRenderer _sr;
        private Quaternion _base;
        private float _dir, _duration, _elapsed;

        internal void Begin(float dir, float duration)
        {
            _sr = GetComponent<SpriteRenderer>();
            _base = transform.rotation;
            _dir = dir >= 0f ? 1f : -1f;
            _duration = Mathf.Max(0.05f, duration);
            _elapsed = 0f;

            // 흔들리던 중에 쓰러질 수 있다 — 흔들림이 각도를 되돌려 놓지 않게 먼저 꺼 둔다.
            var wobble = GetComponent<Wobble>();
            if (wobble != null) { wobble.enabled = false; Destroy(wobble); }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            float fall = Mathf.Clamp01(_elapsed / _duration);
            // t*t = 처음엔 천천히 기울다가 끝에서 확 넘어간다 (실제로 나무가 넘어가는 모양).
            transform.rotation = _base * Quaternion.Euler(0f, 0f, -_dir * FallAngle * fall * fall);

            if (fall < 1f) return;

            float fade = (_elapsed - _duration) / FadeTime;
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = 1f - Mathf.Clamp01(fade);
                _sr.color = c;
            }

            if (fade >= 1f) Destroy(gameObject);
        }
    }
}
