using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Sprites/Cloud 의 구름 그림을 <b>바닥에 드리운 그림자</b>로 맵 위에 흘려보낸다.
    ///
    /// 구름은 월드 좌표에서 바람 방향으로 천천히 흐른다 — 카메라가 움직여도 같이 따라오지 않고
    /// 제자리에서 흘러가므로 정말 하늘에 떠 있는 것처럼 보인다. 화면 밖으로 멀리 나간 구름은
    /// 반대편으로 다시 돌려 보낸다 (카메라 기준으로 감싸기).
    ///
    /// <b>맵에 들어설 때마다</b> 구름을 1~6개 새로 뿌린다. 위치는 화면 안팎 어디든 될 수 있어서
    /// 한쪽에서만 시작하지 않는다. 모든 구름이 같은 속도로 흐르고 처음에 서로 겹치지 않게
    /// 놓으므로 흐르는 동안에도 겹치지 않는다.
    /// 실내에서는 보이지 않고, 해가 지면 서서히 사라지며 아침에 다시 나타난다.
    /// </summary>
    public class CloudShadows : MonoBehaviour
    {
        private const string SpriteFolder = "Sprites/Cloud";

        private const int MinClouds = 6;
        private const int MaxClouds = 12;                     // 기준 크기 맵에서의 최대 개수
        private const int PoolSize = 18;                     // 아주 큰 맵을 위해 만들어 두는 개수
        private const float ReferenceMapArea = 20f * 15f;    // 이 넓이(칸)의 맵에서 최대 MaxClouds개
        private const float Scale = 2.2f;                    // 그림이 작아서 키워 쓴다
        private const float ScaleJitter = 0.15f;             // 구름마다 크기가 ±이만큼 다르다
        private const float Gap = 1.5f;                      // 구름 사이 최소 간격 (월드 칸)
        private const float ShadowAlpha = 0.22f;
        private const float WindSpeed = 0.45f;               // 칸/초
        private static readonly Vector2 WindDir = new Vector2(1f, -0.25f).normalized;
        private const float Margin = 6f;                     // 화면 밖 여유 (월드 칸)
        private const float FadeSpeed = 0.6f;                // 실내·밤 전환 속도 (알파 비율/초)
        private const float CloudFadeSpeed = 0.25f;          // 구름 하나가 나타나는 속도

        private class Cloud
        {
            public SpriteRenderer sr;
            public bool active;
            public float presence;   // 0~1, 이 구름이 지금 얼마나 떠 있는지
            public Vector2 size;     // 월드에서 차지하는 크기
        }

        private readonly List<Cloud> _clouds = new List<Cloud>();
        private Sprite[] _sprites;
        private GameManager _gm;
        private Camera _cam;
        private GameLocation _lastLocation;
        private float _visible;   // 0~1, 지금 보이는 정도

        public void Init(GameManager gm, Camera cam)
        {
            _gm = gm;
            _cam = cam;

            _sprites = Resources.LoadAll<Sprite>(SpriteFolder);
            if (_sprites.Length == 0)
            {
                Debug.LogWarning($"[CloudShadows] {SpriteFolder} 에서 구름 스프라이트를 찾지 못했습니다.");
                enabled = false;
                return;
            }

            var root = new GameObject("CloudShadows").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Cloud{i}");
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = Depth.Overlay - 1000;     // 모든 것 위, UI·연출 표시 아래
                sr.color = Color.clear;
                _clouds.Add(new Cloud { sr = sr });
            }
        }

        private Vector2 HalfExtent()
        {
            float h = _cam.orthographicSize + Margin;
            return new Vector2(h * _cam.aspect + Margin, h);
        }

        /// <summary>구름을 1~6개 새로 뿌린다. 서로 겹치지 않는 자리를 골라, 감싸는 영역 전체에 흩어 놓는다.</summary>
        private void Respawn()
        {
            Vector2 half = HalfExtent();
            Vector2 period = half * 2f;
            Vector3 c = _cam.transform.position;

            // 맵이 넓을수록 구름이 많이 뜬다: 최대 개수가 맵 넓이에 비례한다.
            var loc = _gm.CurrentLocation;
            float area = loc.width * loc.height;
            int maxForMap = Mathf.Clamp(Mathf.RoundToInt(MaxClouds * area / ReferenceMapArea), MinClouds, PoolSize);
            int wanted = Random.Range(MinClouds, maxForMap + 1);

            var placed = new List<Cloud>();
            foreach (var cloud in _clouds)
            {
                cloud.active = false;
                cloud.presence = 0f;
                cloud.sr.color = Color.clear;
            }

            for (int i = 0; i < wanted; i++)
            {
                var cloud = _clouds[i];
                var sprite = _sprites[Random.Range(0, _sprites.Length)];
                float scale = Scale * Random.Range(1f - ScaleJitter, 1f + ScaleJitter);
                cloud.sr.sprite = sprite;
                cloud.sr.flipX = Random.value < 0.5f;
                cloud.sr.transform.localScale = Vector3.one * scale;
                cloud.size = (Vector2)sprite.bounds.size * scale;

                // 겹치지 않는 자리를 찾는다. 몇 번 해도 안 되면 그 구름은 건너뛴다 (영역이 비좁은 경우).
                bool found = false;
                for (int attempt = 0; attempt < 40 && !found; attempt++)
                {
                    var pos = new Vector2(
                        c.x + Random.Range(-half.x, half.x),
                        c.y + Random.Range(-half.y, half.y));
                    if (Overlaps(pos, cloud.size, placed, period)) continue;
                    cloud.sr.transform.position = new Vector3(pos.x, pos.y, 0f);
                    found = true;
                }
                if (!found) continue;

                cloud.active = true;
                placed.Add(cloud);
            }
        }

        /// <summary>pos에 놓은 구름이 이미 놓인 구름과 겹치는지. 감싸는 영역은 이어져 있으므로 반대편 경계도 가깝게 본다.</summary>
        private static bool Overlaps(Vector2 pos, Vector2 size, List<Cloud> others, Vector2 period)
        {
            foreach (var o in others)
            {
                Vector2 d = (Vector2)o.sr.transform.position - pos;
                d.x = Mathf.Abs(d.x); d.y = Mathf.Abs(d.y);
                d.x = Mathf.Min(d.x, period.x - d.x);
                d.y = Mathf.Min(d.y, period.y - d.y);
                if (d.x < (size.x + o.size.x) * 0.5f + Gap && d.y < (size.y + o.size.y) * 0.5f + Gap)
                    return true;
            }
            return false;
        }

        private void Update()
        {
            if (_cam == null || _gm == null || _gm.Data == null || _gm.CurrentLocation == null) return;

            // 맵이 바뀌면 구름을 새로 뿌린다.
            if (_gm.CurrentLocation != _lastLocation)
            {
                _lastLocation = _gm.CurrentLocation;
                Respawn();
            }

            // 목표 가시도: 실내 0, 야간 0, 낮 1. 야외 조명과 같은 연속 시각을 쓴다.
            bool outdoor = !MapGraph.IsIndoor(_gm.CurrentLocation.id);
            float target = outdoor ? OutdoorLightingCycle.Sunlight(_gm.LightingMinutes) : 0f;
            _visible = Mathf.MoveTowards(_visible, target, FadeSpeed * Time.deltaTime);

            Vector2 half = HalfExtent();
            Vector3 cam = _cam.transform.position;
            Vector2 step = WindDir * WindSpeed * Time.deltaTime;

            foreach (var cloud in _clouds)
            {
                if (!cloud.active) continue;
                cloud.presence = Mathf.MoveTowards(cloud.presence, 1f, CloudFadeSpeed * Time.deltaTime);

                var t = cloud.sr.transform;
                var p = t.position;
                p.x += step.x;
                p.y += step.y;

                // 카메라 기준 직사각형을 벗어나면 반대편으로 감싼다.
                if (p.x > cam.x + half.x) p.x -= 2f * half.x;
                else if (p.x < cam.x - half.x) p.x += 2f * half.x;
                if (p.y > cam.y + half.y) p.y -= 2f * half.y;
                else if (p.y < cam.y - half.y) p.y += 2f * half.y;
                t.position = p;

                cloud.sr.color = new Color(0f, 0f, 0f, ShadowAlpha * _visible * cloud.presence);
            }
        }

    }
}
