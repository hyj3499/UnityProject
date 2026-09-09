using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어를 여러 층으로 겹쳐 그리고 동작을 재생한다.
    ///
    /// 그림 한 장이 아니라 <b>피부·옷·눈·머리·장식·도구</b>를 각각의 SpriteRenderer로 겹친다.
    /// 그래서 외형을 바꾸는 것은 층 하나의 경로를 바꾸는 일이고, 동작을 바꾸는 것은
    /// 모든 층이 같은 폴더의 같은 번호 프레임을 보게 하는 일이다.
    ///
    /// 무엇을 재생할지는 세 단계로 정해진다 (위가 우선):
    ///   1. 한 번짜리 동작 — 곡괭이질·물주기처럼 끝나면 저절로 풀린다
    ///   2. 눌러 둔 동작   — 낚시처럼 상태가 유지되는 동안 계속되는 것 (직접 풀어 줘야 한다)
    ///   3. 이동           — 서기/걷기/뛰기 (들고 있으면 들고 있는 판)
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        /// <summary>들고 있는 아이템을 머리 위 어디에 그릴지 (칸 = 1, 화면 위가 +).</summary>
        private const float CarryItemHeight = 0.6f;

        private static readonly PlayerLayer[] DrawOrder =
        {
            PlayerLayer.Skin, PlayerLayer.Clothes, PlayerLayer.Eyes,
            PlayerLayer.Hair, PlayerLayer.Acc, PlayerLayer.Weapon,
        };

        private PlayerAppearance _appearance = new PlayerAppearance();
        private SpriteRenderer[] _layers;
        private SpriteRenderer _carriedItem;

        private PlayerAnim _current = PlayerAnim.Idle;
        private string _currentWeapon;
        private Direction _facing = Direction.Down;

        private float _timer;
        private int _frame;
        private int _frameCount;
        private bool _finished;

        // 한 번짜리 동작 / 눌러 둔 동작
        private bool _hasOneShot;
        private PlayerAnim _oneShot;
        private string _oneShotWeapon;

        private bool _hasOverride;
        private PlayerAnim _override;
        private string _overrideWeapon;

        // 이동 상태 (바깥에서 매 프레임 알려 준다)
        private bool _moving, _running, _carrying;

        /// <summary>한 번짜리 동작이 아직 재생 중인지 (움직임을 잠깐 막고 싶을 때 본다).</summary>
        public bool IsPlayingOneShot => _hasOneShot;

        public PlayerAppearance Appearance => _appearance;

        public void Init(PlayerAppearance appearance)
        {
            _appearance = appearance != null ? appearance.Clone() : new PlayerAppearance();

            _layers = new SpriteRenderer[DrawOrder.Length];
            for (int i = 0; i < DrawOrder.Length; i++)
                _layers[i] = MakeRenderer(DrawOrder[i].ToString(), i + 1);

            _carriedItem = MakeRenderer("CarriedItem", DrawOrder.Length + 1);
            _carriedItem.transform.localPosition = new Vector3(0f, CarryItemHeight, 0f);
            _carriedItem.enabled = false;

            Rebuild();
        }

        private SpriteRenderer MakeRenderer(string name, int subOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // 같은 칸 안에서의 앞뒤 순서. Depth.YSort는 칸마다 100씩 벌어져 있어 자리가 넉넉하다.
            sr.sortingOrder = subOrder;
            return sr;
        }

        /// <summary>외형을 갈아 끼운다 (캐릭터 만들기 화면의 미리보기가 매번 부른다).</summary>
        public void SetAppearance(PlayerAppearance appearance)
        {
            _appearance = appearance != null ? appearance.Clone() : new PlayerAppearance();
            Rebuild();
        }

        // ---------- 무엇을 재생할지 ----------

        /// <summary>매 프레임 이동 상태를 알려 준다. 실제로 무엇이 재생될지는 우선순위가 정한다.</summary>
        public void SetLocomotion(bool moving, bool running, bool carrying, Direction facing)
        {
            _moving = moving;
            _running = running;
            _carrying = carrying;
            _facing = facing;
        }

        /// <summary>곡괭이질·물주기처럼 한 번 재생하고 저절로 풀리는 동작.</summary>
        public void PlayOnce(PlayerAnim anim, string weapon = null)
        {
            _hasOneShot = true;
            _oneShot = anim;
            _oneShotWeapon = weapon;
            Apply(anim, weapon, restart: true);
        }

        /// <summary>낚시처럼 상태가 유지되는 동안 계속 재생할 동작. ClearOverride로 푼다.</summary>
        public void SetOverride(PlayerAnim anim, string weapon = null)
        {
            bool changed = !_hasOverride || _override != anim || _overrideWeapon != weapon;
            _hasOverride = true;
            _override = anim;
            _overrideWeapon = weapon;
            if (changed && !_hasOneShot) Apply(anim, weapon, restart: true);
        }

        public void ClearOverride() => _hasOverride = false;

        /// <summary>머리 위에 들고 있을 아이템 그림 (없으면 null).</summary>
        public void SetCarriedItem(Sprite sprite)
        {
            if (_carriedItem == null) return;
            _carriedItem.sprite = sprite;
            _carriedItem.enabled = sprite != null;
        }

        /// <summary>이 칸의 앞뒤 순서. 층들은 그 위에서 조금씩 더 앞으로 그려진다.</summary>
        public void SetSortingOrder(int order)
        {
            if (_layers == null) return;
            for (int i = 0; i < _layers.Length; i++)
                if (_layers[i] != null) _layers[i].sortingOrder = order + i + 1;
            if (_carriedItem != null) _carriedItem.sortingOrder = order + _layers.Length + 1;
        }

        // ---------- 재생 ----------
        private void LateUpdate() => Advance(Time.deltaTime);

        private void Advance(float dt)
        {
            if (_layers == null) return;

            // 한 번짜리가 끝났으면 풀어 준다 — 그다음 우선순위가 저절로 이어받는다.
            if (_hasOneShot && _finished) _hasOneShot = false;

            PlayerAnim want = _hasOneShot ? _oneShot
                            : _hasOverride ? _override
                            : PlayerAnimations.ForLocomotion(_moving, _running, _carrying);
            string weapon = _hasOneShot ? _oneShotWeapon
                          : _hasOverride ? _overrideWeapon
                          : null;

            Apply(want, weapon, restart: false);

            var def = PlayerAnimations.Get(_current);
            if (_frameCount <= 0) return;

            if (_finished) { Draw(); return; }

            _timer += dt;
            float step = 1f / Mathf.Max(1f, def.fps);
            while (_timer >= step)
            {
                _timer -= step;
                _frame++;
                if (_frame >= _frameCount)
                {
                    if (def.loop) _frame = 0;
                    else { _frame = _frameCount - 1; _finished = true; break; }
                }
            }
            Draw();
        }

        /// <summary>재생할 동작을 바꾼다. 같은 동작이면 프레임을 이어서 재생한다.</summary>
        private void Apply(PlayerAnim anim, string weapon, bool restart)
        {
            string wanted = weapon ?? PlayerAnimations.Get(anim).weapon;
            if (!restart && anim == _current && wanted == _currentWeapon) return;

            _current = anim;
            _currentWeapon = wanted;
            _frameCount = PlayerSpriteLibrary.FrameCount(PlayerAnimations.Get(anim).folder);
            _frame = 0;
            _timer = 0f;
            _finished = false;
        }

        private void Draw()
        {
            var def = PlayerAnimations.Get(_current);
            for (int i = 0; i < DrawOrder.Length; i++)
            {
                var sr = _layers[i];
                if (sr == null) continue;

                var layer = DrawOrder[i];
                Sprite[] frames = layer == PlayerLayer.Weapon
                    ? WeaponFrames(def.folder)
                    : layer == PlayerLayer.Acc
                        ? AccFrames(def.folder)
                        : ColorLayerFrames(def.folder, layer);

                if (frames == null || frames.Length == 0)
                {
                    sr.enabled = false;
                    continue;
                }

                sr.sprite = frames[Mathf.Clamp(_frame, 0, frames.Length - 1)];
                sr.enabled = true;
            }
        }

        private Sprite[] WeaponFrames(string animFolder)
            => _currentWeapon == null ? null : PlayerSpriteLibrary.Frames(animFolder, _currentWeapon, _facing);

        private Sprite[] AccFrames(string animFolder)
        {
            string path = _appearance.PathFor(PlayerLayer.Acc);
            // 장식은 동작마다 다 갖춰져 있지 않다 — 없으면 그냥 안 그린다 (다른 색으로 대신하지 않는다).
            return path == null ? null : PlayerSpriteLibrary.Frames(animFolder, path, _facing, allowSibling: false);
        }

        /// <summary>
        /// 피부·옷·눈·머리는 사용자가 고른 색(HEX)을 PlayerPaletteSwap으로 입힌다 — 파일을 그대로
        /// 찾는 게 아니라, 그 폴더의 밑그림 하나를 골라 색만 다시 계산한다.
        /// </summary>
        private Sprite[] ColorLayerFrames(string animFolder, PlayerLayer layer)
        {
            string folder = _appearance.FolderFor(layer);
            if (folder == null) return null;

            var (presetA, presetB) = ReferencePair(layer);
            Color target = TargetColor(layer);
            return PlayerPaletteSwap.Frames(animFolder, folder, presetA, presetB, _facing, target);
        }

        private (string, string) ReferencePair(PlayerLayer layer)
        {
            switch (layer)
            {
                case PlayerLayer.Skin: return ("1", "2");
                case PlayerLayer.Clothes: return ("Blue", "Green");
                case PlayerLayer.Eyes: return ("Black", "Blue");
                case PlayerLayer.Hair: return ("Brown", "Black");
                default: return (null, null);
            }
        }

        private Color TargetColor(PlayerLayer layer)
        {
            switch (layer)
            {
                case PlayerLayer.Skin: return _appearance.SkinColor;
                case PlayerLayer.Clothes: return _appearance.ClothesColorValue;
                case PlayerLayer.Eyes: return _appearance.EyeColorValue;
                case PlayerLayer.Hair: return _appearance.HairColorValue;
                default: return Color.white;
            }
        }

        /// <summary>외형이 바뀌었을 때 지금 프레임을 곧바로 다시 그린다.</summary>
        private void Rebuild()
        {
            if (_layers == null) return;
            _frameCount = PlayerSpriteLibrary.FrameCount(PlayerAnimations.Get(_current).folder);
            Draw();
        }
    }
}
