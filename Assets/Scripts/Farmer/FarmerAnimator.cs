using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어를 그리고 동작을 재생한다 (스타듀밸리와 같은 방식).
    ///
    /// 한 프레임은 <b>번호 하나</b>로 정해진다. 그 번호로 몸·바지·셔츠·머리카락·팔을 한꺼번에 꺼내
    /// 아래에서 위로 겹쳐 그린다. 팔이 맨 위라서 소매가 팔 동작을 그대로 따라간다.
    /// 동작을 바꾼다는 건 "어떤 번호들을 몇 초씩 보여줄지"를 바꾸는 것뿐이라(FarmerAnimations),
    /// 새 동작을 넣는 데 그림이 필요 없다.
    ///
    /// 무엇을 재생할지는 세 단계로 정해진다 (위가 우선):
    ///   1. 한 번짜리 동작 — 곡괭이질·물주기처럼 끝나면 저절로 풀린다
    ///   2. 눌러 둔 동작   — 낚시처럼 상태가 유지되는 동안 계속되는 것
    ///   3. 이동           — 서기/걷기/뛰기
    /// </summary>
    public class FarmerAnimator : MonoBehaviour
    {
        /// <summary>
        /// 그림을 얼마나 위로 올려 그릴지. 스프라이트가 16x32(=1x2칸)라 가운데를 기준으로 두면
        /// 발이 한 칸 아래로 파묻힌다. 반 칸 올려 서 있는 칸의 아래 모서리에 발을 맞춘다.
        /// </summary>
        private const float FeetOffset = 0.5f;

        /// <summary>들고 있는 아이템을 머리 위 어디에 그릴지.</summary>
        private const float CarryItemHeight = 1.1f;

        private PlayerAppearance _appearance = new PlayerAppearance();

        // 겹치는 순서 (아래에서 위로). 팔이 맨 위라 소매가 팔 동작을 그대로 따라간다.
        private SpriteRenderer _body, _pants, _shirt, _hair, _arms, _carriedItem;

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

        /// <summary>한 번짜리 동작이 아직 재생 중인지.</summary>
        public bool IsPlayingOneShot => _hasOneShot;

        public PlayerAppearance Appearance => _appearance;

        /// <summary>무언가를 들고 있으면 팔이 머리 위로 올라간 그림을 쓴다.</summary>
        private bool IsCarrying => _carriedItem != null && _carriedItem.sprite != null;

        public void Init(PlayerAppearance appearance)
        {
            _appearance = appearance != null ? appearance.Clone() : new PlayerAppearance();

            _body = MakeRenderer("Body", 1);
            _pants = MakeRenderer("Pants", 2);
            _shirt = MakeRenderer("Shirt", 3);
            _hair = MakeRenderer("Hair", 4);
            _arms = MakeRenderer("Arms", 5);

            _carriedItem = MakeRenderer("CarriedItem", 6);
            _carriedItem.transform.localPosition = new Vector3(0f, CarryItemHeight, 0f);
            _carriedItem.enabled = false;

            Play(FarmerAnim.Idle, restart: true);
            Draw();
        }

        private SpriteRenderer MakeRenderer(string name, int subOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
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
            if (_body == null) return;
            _body.sortingOrder = order + 1;
            _pants.sortingOrder = order + 2;
            _shirt.sortingOrder = order + 3;
            _hair.sortingOrder = order + 4;
            _arms.sortingOrder = order + 5;
            _carriedItem.sortingOrder = order + 6;
        }

        // ---------- 재생 ----------
        private void LateUpdate() => Advance(Time.deltaTime);

        private void Advance(float dt)
        {
            if (_body == null) return;

            if (_hasOneShot && _finished) _hasOneShot = false;

            var want = _hasOneShot ? _oneShot
                     : _hasOverride ? _override
                     : FarmerAnimations.ForLocomotion(_moving, _running);

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

        /// <summary>재생할 동작을 바꾼다. 같은 동작·같은 방향이면 이어서 재생한다.</summary>
        private void Play(FarmerAnim anim, bool restart)
        {
            var clip = FarmerAnimations.Get(anim, _facing);
            if (!restart && anim == _current && clip == _clip) return;

            _current = anim;
            _clip = clip;
            _key = 0;
            _timer = 0f;
            _finished = false;
        }

        private void Draw()
        {
            if (_body == null || _clip == null || _clip.keys.Length == 0) return;

            int frame = _clip.keys[Mathf.Clamp(_key, 0, _clip.keys.Length - 1)].frame;
            bool mirrored = FarmerAnimations.Mirrored(_facing);
            var palette = _appearance.ToPalette();
            bool female = _appearance.IsFemale;
            var armSection = IsCarrying ? FarmerSection.ArmsCarrying : FarmerSection.Arms;

            Set(_body, FarmerSheet.Get(female, FarmerSection.Body, frame, palette), Color.white, mirrored);
            Set(_pants, FarmerClothes.Pants(frame), _appearance.PantsColor, mirrored);
            Set(_shirt, FarmerClothes.Shirt(_appearance.shirtStyle, frame), _appearance.ShirtColor, mirrored);
            Set(_hair, FarmerClothes.Hair(_appearance.hairStyle, frame), _appearance.HairColor, mirrored);
            Set(_arms, FarmerSheet.Get(female, armSection, frame, palette), Color.white, mirrored);
        }

        /// <summary>한 층을 그린다. 옷은 흰 그림이라 색을 곱해 물들인다.</summary>
        private static void Set(SpriteRenderer sr, Sprite sprite, Color tint, bool mirrored)
        {
            if (sr == null) return;
            if (sprite == null) { sr.enabled = false; return; }

            sr.sprite = sprite;
            sr.color = tint;
            sr.flipX = mirrored;
            sr.enabled = true;
        }
    }
}
