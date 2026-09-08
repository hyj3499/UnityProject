using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// 한 타일셋 시트가 애니메이션을 어떻게 담고 있는지에 대한 설명.
    ///
    /// 이 에셋 팩은 스프라이트 이름 번호로 애니메이션 프레임을 묶는다.
    /// 예를 들어 "Water Ground animations tiles"는 93개 단위로 같은 칸의 다음 프레임을 둔다:
    ///   _0 -> _93 -> _186 -> _279
    /// 그래서 시트마다 (프레임 번호 간격, 장수, 재생 방식, 속도)만 적어 두면 프레임을 계산으로 찾을 수 있고,
    /// 애니메이션 타일 에셋을 손으로 만들 필요가 없다.
    /// </summary>
    public class TileAnimationDef
    {
        /// <summary>시트 파일 이름 (계절 낱말은 빼고 적어도 된다 — 포함 여부로 찾는다).</summary>
        public string sheetName;

        /// <summary>같은 칸의 다음 프레임까지 스프라이트 이름 번호가 얼마나 증가하는지.</summary>
        public int frameIndexStride;

        /// <summary>몇 장짜리인지.</summary>
        public int frameCount;

        /// <summary>끝 프레임 뒤에 중간 프레임을 역순으로 재생할지.</summary>
        public bool pingPong;

        /// <summary>초당 프레임 수.</summary>
        public float fps = 4f;
    }

    /// <summary>
    /// 어떤 시트가 애니메이션 타일을 담고 있는지 모아 둔 표.
    /// 시트를 하나 더 쓰게 되면 여기 Register 한 줄만 더하면 된다 — 물, 용암, 폭포 전부 같은 방식이다.
    /// </summary>
    public static class TileAnimationDatabase
    {
        private static readonly List<TileAnimationDef> _defs = new List<TileAnimationDef>();
        private static bool _init;

        public static void Init()
        {
            if (_init) return;
            _init = true;

            // 물결. _0 -> _93 -> _186 -> _279, 이후 _186 -> _93으로 되돌아오는 핑퐁 재생.
            Register(new TileAnimationDef
            {
                sheetName = "Water Ground animations tiles",
                frameIndexStride = 93,
                frameCount = 4,
                pingPong = true,
                fps = 4f,
            });

            // 시트를 더 쓰면 여기에:
            //   Register(new TileAnimationDef { sheetName = "Beach animations tiles", ... });
            //   Register(new TileAnimationDef { sheetName = "Waterfall", ... });
        }

        private static void Register(TileAnimationDef def) => _defs.Add(def);

        /// <summary>이 시트가 애니메이션 시트인지. 아니면 null.</summary>
        public static TileAnimationDef ForSheet(string sheetName)
        {
            Init();
            if (string.IsNullOrEmpty(sheetName)) return null;
            foreach (var d in _defs)
                if (sheetName.Contains(d.sheetName)) return d;
            return null;
        }

    }
}
