using System.Collections.Generic;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// 플레이어를 그리고 동작을 재생한다.
    ///
    /// 그림은 <b>Base/base_animations.png</b> 한 장이다. 모든 동작이 이미 합쳐진 채로 그려져
    /// 있어서 그리는 일은 <b>그 시트에서 칸 하나를 골라 놓는 것</b>이 전부다. 부위를 따로 불러
    /// 겹치지 않으므로 층도, 부위별 칸 번호도, 통통 튀게 하려고 자리를 올리던 장치도 없다 —
    /// 튀는 것까지 그림에 들어 있다.
    ///
    /// 어느 줄을 몇 칸씩 넘길지는 전부 동작표에 적혀 있다(FarmerPoses). 여기서는 그 줄을
    /// 시간에 맞춰 넘기기만 한다.
    ///
    /// 무엇을 재생할지는 세 단계로 정해진다 (위가 우선):
    ///   1. 한 번짜리 동작 — 곡괭이질·물주기처럼 끝나면 저절로 풀린다
    ///   2. 눌러 둔 동작   — 낚시처럼 상태가 유지되는 동안 계속되는 것
    ///   3. 이동           — 서기/걷기/뛰기 (무언가 들고 있으면 드는 자세)
    /// </summary>
    public class FarmerAnimator : MonoBehaviour
    {
        private const float PixelsPerUnit = FarmerPoses.PixelsPerUnit;

        /// <summary>
        /// 그림을 얼마나 올려 그릴지. 그림의 축은 칸 한가운데에 있고 발바닥은 칸 맨 아래에
        /// 있어서, 그 차이만큼 올려 주면 서 있는 타일의 아래 모서리에 발이 닿는다.
        /// </summary>
        private const float FeetOffset = FarmerPoses.FeetBelowCenter / PixelsPerUnit - 0.5f;

        /// <summary>들고 있는 아이템을 머리 위 어디에 그릴지.</summary>
        private const float CarryItemHeight = 1.0f;

        /// <summary>몸과 든 것의 앞뒤 순서. 사이를 띄워 나중에 끼워 넣을 자리를 남겨 둔다.</summary>
        private const int BodyOrder = 2, CarryOrder = 4;

        private PlayerAppearance _appearance = new PlayerAppearance();

        private SpriteRenderer _body;
        private SpriteRenderer _carriedItem;

        private Direction _facing = Direction.Down;
        private FarmerAnim _current = FarmerAnim.Idle;
        private FarmerClip _clip;
        private int _frame;
        private float _timer;
        private bool _finished;

        private bool _hasOneShot;
        private FarmerAnim _oneShot;

        private bool _hasOverride;
        private FarmerAnim _override;

        private bool _moving, _running;

        /// <summary>이 칸의 앞뒤 순서. 몸과 든 것은 이 값 위에 얹힌다.</summary>
        private int _sortingBase;

        public bool IsPlayingOneShot => _hasOneShot;
        public PlayerAppearance Appearance => _appearance;

        private bool IsCarrying => _carriedItem != null && _carriedItem.sprite != null;

        public void Init(PlayerAppearance appearance)
        {
            _appearance = appearance != null ? appearance.Clone() : new PlayerAppearance();

            _body = MakeRenderer("Body", BodyOrder);
            _body.transform.localPosition = new Vector3(0f, FeetOffset, 0f);

            // 든 것은 손에 딸려 있으니 몸과 같은 자리에서 머리 위로 올려 둔다.
            _carriedItem = MakeRenderer("CarriedItem", CarryOrder);
            _carriedItem.transform.localPosition = new Vector3(0f, FeetOffset + CarryItemHeight, 0f);
            _carriedItem.enabled = false;

            Play(FarmerAnim.Idle, restart: true);
            Draw();
        }

        private SpriteRenderer MakeRenderer(string name, int subOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // 같은 칸 안에서의 앞뒤 순서. Depth.YSort는 칸마다 100씩 벌어져 있어 자리가 넉넉하다.
            sr.sortingOrder = _sortingBase + subOrder;
            return sr;
        }

        /// <summary>
        /// 외형을 갈아 끼운다.
        ///
        /// 지금 그림은 모든 동작이 합쳐진 한 장뿐이라 고를 것이 없다 — 받아만 두고 그리는 데는
        /// 쓰지 않는다. 옷·머리 모양을 다시 갈아입힐 수 있게 되면 여기서 시트를 바꾸면 된다.
        /// </summary>
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

        /// <summary>이 칸의 앞뒤 순서. 몸과 든 것은 그 위에서 조금씩 더 앞으로 그려진다.</summary>
        public void SetSortingOrder(int order)
        {
            _sortingBase = order;
            if (_body != null) _body.sortingOrder = order + BodyOrder;
            if (_carriedItem != null) _carriedItem.sortingOrder = order + CarryOrder;
        }

        // ---------- 재생 ----------
        private void LateUpdate() => Advance(Time.deltaTime);

        private void Advance(float dt)
        {
            if (_body == null) return;

            if (_hasOneShot && _finished) _hasOneShot = false;

            var want = _hasOneShot ? _oneShot
                     : _hasOverride ? _override
                     : Locomotion();

            Play(want, restart: false);

            // 동작표를 손으로 고치다 0을 적어도 여기서 멈추지 않도록 막아 둔다.
            if (_clip == null || _clip.count <= 0 || _clip.ms <= 0) return;

            if (!_finished)
            {
                _timer += dt * 1000f;
                while (_timer >= _clip.ms)
                {
                    _timer -= _clip.ms;
                    _frame++;
                    if (_frame >= _clip.count)
                    {
                        if (_clip.loop) _frame = 0;
                        else { _frame = _clip.count - 1; _finished = true; break; }
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
            _frame = 0;
            _timer = 0f;
            _finished = false;
        }

        private void Draw()
        {
            if (_body == null || _clip == null || _clip.count <= 0) return;

            var frames = Sheet.Row(_clip.row);
            if (frames == null) { _body.enabled = false; return; }

            int frame = Mathf.Clamp(_frame, 0, Mathf.Min(_clip.count, frames.Length) - 1);
            if (frame < 0) { _body.enabled = false; return; }

            _body.sprite = frames[frame];
            // 왼쪽 그림은 따로 없다 — 측면 줄을 뒤집어 쓴다.
            _body.flipX = FarmerPoses.Mirrored(_facing);
            _body.enabled = true;

            if (_carriedItem != null) _carriedItem.flipX = false;
        }

        /// <summary>
        /// 동작 시트를 줄 단위로 잘라 두는 곳.
        ///
        /// 시트는 한 장뿐이고 모두가 같은 것을 보므로 한 번만 잘라 나눠 쓴다. 축은 칸 한가운데로
        /// 잡는다 — 칸마다 몸이 이미 제자리에 그려져 있어서 자리를 따로 맞출 일이 없다.
        /// </summary>
        private static class Sheet
        {
            private static Sprite[][] _rows;
            private static bool _tried;

            /// <summary>그 줄의 칸들. 시트가 없으면 null.</summary>
            public static Sprite[] Row(int row)
            {
                if (!_tried) Load();
                if (_rows == null || row < 0 || row >= _rows.Length) return null;
                return _rows[row];
            }

            private static void Load()
            {
                _tried = true;

                var tex = Resources.Load<Texture2D>(FarmerPoses.SheetPath);
                if (tex == null)
                {
                    Debug.LogWarning($"[FarmerAnimator] {FarmerPoses.SheetPath} 를 찾지 못했습니다.");
                    return;
                }

                int fw = FarmerPoses.FrameWidth, fh = FarmerPoses.FrameHeight;
                int rows = Mathf.Min(tex.height / fh, FarmerPoses.RowCount);
                int cols = Mathf.Min(tex.width / fw, FarmerPoses.Columns);

                if (rows < FarmerPoses.RowCount)
                    Debug.LogWarning($"[FarmerAnimator] {FarmerPoses.SheetPath} 는 줄이 {rows}개뿐입니다. " +
                                     $"동작표는 {FarmerPoses.RowCount}줄을 기대합니다.");

                var pivot = new Vector2(0.5f, 0.5f);
                var made = new List<Sprite[]>(rows);

                for (int r = 0; r < rows; r++)
                {
                    var line = new Sprite[cols];
                    for (int c = 0; c < cols; c++)
                    {
                        // 그림은 위에서부터 줄을 세지만 텍스처는 아래가 0이라 뒤집어 읽는다.
                        var rect = new Rect(c * fw, tex.height - (r + 1) * fh, fw, fh);
                        line[c] = Sprite.Create(tex, rect, pivot, FarmerPoses.PixelsPerUnit,
                                                0, SpriteMeshType.FullRect);
                        line[c].name = $"{tex.name}_{r}_{c}";
                    }
                    made.Add(line);
                }
                _rows = made.ToArray();
            }
        }
    }
}
