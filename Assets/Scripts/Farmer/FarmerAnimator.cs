using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어를 그리고 동작을 재생한다.
    ///
    /// 그림은 부위(머리·몸통·다리·양팔·머리카락·옷…)마다 따로 있고, 한 순간의 모습은 부위별
    /// 그림 번호를 모아 놓은 <b>동작표의 한 칸</b>으로 정해진다. 그 칸대로 부위마다 한 장씩
    /// 꺼내 아래에서 위로 겹치면 그 자세가 된다.
    /// 부위 그림은 전부 같은 크기의 칸에 서로 자리가 맞도록 그려져 있어서(FarmerSlots), 층을
    /// 같은 자리에 놓고 겹치기만 하면 몸이 된다.
    ///
    /// 어느 부위가 몇 번 그림을 쓸지는 전부 동작표에 적혀 있다(FarmerPoses). 여기서는 그 표를
    /// 시간에 맞춰 넘기고, 칸마다 부위별 번호를 꺼내 그리기만 한다.
    ///
    /// 걸을 때 통통 튀는 것은 그림이 아니라 <b>자리</b>로 낸다. 층들은 두 겹의 덩어리에 매달려
    /// 있고, 동작표가 칸마다 그 둘을 몇 픽셀씩 올릴지 적어 둔다:
    ///   · Body      — 몸 전체(다리·발 포함). 올리면 발이 땅에서 떨어져 폴짝 뛴다   (hop)
    ///   · UpperBody — 그 안에서 다리·발을 뺀 나머지. 올리면 허리만 늘어난다      (bounce)
    /// 상체가 몸 안에 들어 있어 둘은 저절로 합쳐진다. 머리에 붙은 눈·머리카락·모자는 머리와
    /// 같은 덩어리라 언제나 머리가 움직인 만큼 따라간다.
    ///
    /// 둘은 따로 껐다 켤 수 있다(useHop·useWaistStretch). 허리 늘이기는 늘어난 자리에 다리
    /// 그림의 골반이 드러나야 하므로, 그 자리가 칠해진 그림에서만 켠다.
    ///
    /// 무엇을 재생할지는 세 단계로 정해진다 (위가 우선):
    ///   1. 한 번짜리 동작 — 곡괭이질·물주기처럼 끝나면 저절로 풀린다
    ///   2. 눌러 둔 동작   — 낚시처럼 상태가 유지되는 동안 계속되는 것
    ///   3. 이동           — 서기/걷기/뛰기 (무언가 들고 있으면 드는 자세)
    /// </summary>
    public class FarmerAnimator : MonoBehaviour
    {
        private const float PixelsPerUnit = FarmerSlots.PixelsPerUnit;

        /// <summary>
        /// 그림을 얼마나 올려 그릴지. 층의 축은 몸 칸 한가운데에 있고 발바닥은 그보다 조금
        /// 아래에 있어서, 그 차이만큼 올려 주면 서 있는 칸의 아래 모서리에 발이 닿는다.
        /// </summary>
        private const float FeetOffset = FarmerSlots.FeetBelowCenter / PixelsPerUnit - 0.5f;

        /// <summary>들고 있는 아이템을 머리 위 어디에 그릴지.</summary>
        private const float CarryItemHeight = 1.0f;

        [Header("걸을 때 몸을 움직이는 방식 (섞어 쓸 수 있다)")]

        [Tooltip("다리와 발까지 몸을 통째로 띄운다. 이음매가 벌어지지 않아 그림을 고칠 필요가 없다.")]
        public bool useHop = true;

        [Tooltip("다리는 땅에 두고 상체만 올려 허리를 늘린다. " +
                 "다리 그림 위쪽에 골반이 미리 칠해져 있어야 허리에 빈 줄이 생기지 않는다.")]
        public bool useWaistStretch = true;

        private PlayerAppearance _appearance = new PlayerAppearance();

        private SpriteRenderer[] _layers;      // FarmerSlots.DrawOrder 와 같은 순서
        private SpriteRenderer _carriedItem;

        /// <summary>몸 전체를 묶어 둔 자리. 여기를 올리면 다리·발까지 함께 떠오른다.</summary>
        private Transform _body;

        /// <summary>몸 안에서 다리·발을 뺀 나머지. 여기만 올리면 허리가 늘어난다.</summary>
        private Transform _upperBody;

        private Direction _facing = Direction.Down;
        private FarmerAnim _current = FarmerAnim.Idle;
        private FarmerClip _clip;
        private int _key;
        private float _timer;
        private bool _finished;

        private bool _hasOneShot;
        private FarmerAnim _oneShot;

        private bool _hasOverride;
        private FarmerAnim _override;

        private bool _moving, _running;

        /// <summary>이 칸의 앞뒤 순서. 층들은 이 값 위에 얹힌다.</summary>
        private int _sortingBase;

        public bool IsPlayingOneShot => _hasOneShot;
        public PlayerAppearance Appearance => _appearance;

        private bool IsCarrying => _carriedItem != null && _carriedItem.sprite != null;

        public void Init(PlayerAppearance appearance)
        {
            _appearance = appearance != null ? appearance.Clone() : new PlayerAppearance();

            // 상체를 몸 안에 넣어 두면 두 움직임이 저절로 합쳐진다.
            // 몸이 1 오르고 상체가 1 더 오르면 다리는 1, 머리는 2 오른 셈이 된다.
            _body = MakeJoint("Body", transform);
            _upperBody = MakeJoint("UpperBody", _body);

            var order = FarmerSlots.DrawOrder;
            _layers = new SpriteRenderer[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                var parent = Grounded(FarmerSlots.Info(order[i]).part) ? _body : _upperBody;
                _layers[i] = MakeRenderer(order[i].ToString(), LayerOrder(i), parent);
            }

            // 든 것은 손에 딸려 있으니 상체를 따라 움직인다.
            _carriedItem = MakeRenderer("CarriedItem", LayerOrder(order.Length), _upperBody);
            _carriedItem.transform.localPosition = new Vector3(0f, CarryItemHeight, 0f);
            _carriedItem.enabled = false;

            Play(FarmerAnim.Idle, restart: true);
            Draw();
        }

        /// <summary>층 사이를 한 칸씩 띄워 둔다. 나중에 층을 끼워 넣을 자리를 남겨 두는 것이다.</summary>
        private static int LayerOrder(int index) => 2 * (index + 1);

        /// <summary>바닥을 딛는 부위. 허리가 늘어날 때 제자리에 남는다. 나머지는 상체에 얹힌다.</summary>
        private static bool Grounded(FarmerPart part) => part == FarmerPart.Legs;

        /// <summary>층을 매달아 두고 통째로 움직이기 위한 빈 자리.</summary>
        private static Transform MakeJoint(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private SpriteRenderer MakeRenderer(string name, int subOrder, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, FeetOffset, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            // 같은 칸 안에서의 앞뒤 순서. Depth.YSort는 칸마다 100씩 벌어져 있어 자리가 넉넉하다.
            sr.sortingOrder = subOrder;
            return sr;
        }

        public void SetAppearance(PlayerAppearance appearance)
        {
            _appearance = appearance != null ? appearance.Clone() : new PlayerAppearance();
            Draw();
        }

        // ---------- 무엇을 재생할지 ----------
        public void SetLocomotion(bool moving, bool running, Direction facing)
        {
            _moving = moving;
            _running = running;
            _facing = facing;
        }

        /// <summary>곡괭이질·물주기처럼 한 번 재생하고 저절로 풀리는 동작.</summary>
        public void PlayOnce(FarmerAnim anim)
        {
            _hasOneShot = true;
            _oneShot = anim;
            Play(anim, restart: true);
        }

        /// <summary>낚시처럼 상태가 유지되는 동안 계속 재생할 동작.</summary>
        public void SetOverride(FarmerAnim anim)
        {
            bool changed = !_hasOverride || _override != anim;
            _hasOverride = true;
            _override = anim;
            if (changed && !_hasOneShot) Play(anim, restart: true);
        }

        public void ClearOverride() => _hasOverride = false;

        public void SetCarriedItem(Sprite sprite)
        {
            if (_carriedItem == null) return;
            _carriedItem.sprite = sprite;
            _carriedItem.enabled = sprite != null;
        }

        /// <summary>이 칸의 앞뒤 순서. 층들은 그 위에서 조금씩 더 앞으로 그려진다.</summary>
        public void SetSortingOrder(int order)
        {
            _sortingBase = order;
            if (_layers == null) return;
            for (int i = 0; i < _layers.Length; i++)
                if (_layers[i] != null) _layers[i].sortingOrder = order + LayerOrder(i);
            if (_carriedItem != null) _carriedItem.sortingOrder = order + LayerOrder(_layers.Length);
        }

        // ---------- 재생 ----------
        private void LateUpdate() => Advance(Time.deltaTime);

        private void Advance(float dt)
        {
            if (_layers == null) return;

            if (_hasOneShot && _finished) _hasOneShot = false;

            var want = _hasOneShot ? _oneShot
                     : _hasOverride ? _override
                     : Locomotion();

            Play(want, restart: false);

            if (_clip == null || _clip.keys.Length == 0) return;

            if (!_finished)
            {
                _timer += dt * 1000f;
                while (_timer >= _clip.keys[_key].ms)
                {
                    _timer -= _clip.keys[_key].ms;
                    _key++;
                    if (_key >= _clip.keys.Length)
                    {
                        if (_clip.loop) _key = 0;
                        else { _key = _clip.keys.Length - 1; _finished = true; break; }
                    }
                }
            }
            Draw();
        }

        /// <summary>손에 뭘 들었으면 드는 자세, 아니면 서기/걷기/뛰기.</summary>
        private FarmerAnim Locomotion()
        {
            if (IsCarrying && !_moving) return FarmerAnim.Carry;
            return FarmerPoses.ForLocomotion(_moving, _running);
        }

        private void Play(FarmerAnim anim, bool restart)
        {
            var clip = FarmerPoses.Get(anim, _facing);
            if (!restart && anim == _current && clip == _clip) return;

            _current = anim;
            _clip = clip;
            _key = 0;
            _timer = 0f;
            _finished = false;
        }

        private void Draw()
        {
            if (_layers == null || _clip == null || _clip.keys.Length == 0) return;

            var key = _clip.keys[Mathf.Clamp(_key, 0, _clip.keys.Length - 1)];
            bool mirrored = FarmerPoses.Mirrored(_facing);
            int facingFrame = FarmerPoses.FacingFrame(_facing);
            var order = FarmerSlots.DrawOrder;

            _body.localPosition = new Vector3(0f, (useHop ? key.hop : 0) / PixelsPerUnit, 0f);
            _upperBody.localPosition = new Vector3(0f, (useWaistStretch ? key.bounce : 0) / PixelsPerUnit, 0f);

            for (int i = 0; i < order.Length; i++)
            {
                var sr = _layers[i];
                if (sr == null) continue;

                var slot = order[i];
                var frames = FarmerArt.Frames(_appearance.ItemFor(slot), slot, _appearance.TintFor(slot));
                if (frames == null || frames.Length == 0) { sr.enabled = false; continue; }

                // 방향별 그림이 따로 있는 부위는 동작표가 아니라 바라보는 쪽이 번호를 정하고,
                // 왼쪽 그림을 이미 가지고 있으니 뒤집지도 않는다.
                var info = FarmerSlots.Info(slot);
                bool byFacing = info.frames == FarmerFrameSource.Facing;

                int frame = byFacing ? facingFrame : key.FrameFor(info.part);
                if (frame < 0 || frame >= frames.Length) { sr.enabled = false; continue; }

                sr.sprite = frames[frame];
                sr.flipX = mirrored && !byFacing;
                sr.enabled = true;
            }

            if (_carriedItem != null) _carriedItem.flipX = false;
        }
    }
}
