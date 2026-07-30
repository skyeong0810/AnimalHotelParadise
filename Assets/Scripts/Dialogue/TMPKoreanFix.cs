using UnityEngine;
using TMPro;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 대화 Bubble에서 쓰는 TextMeshPro(3D, World Space) 텍스트들이 공통으로 겪는 두 가지 문제를
    /// 한 곳에서 처리한다.
    ///
    /// 1) Font Asset이 명시적으로 지정되지 않은 텍스트 — 특히 <see cref="StaffCombinedBubble"/>이
    ///    선택지마다 런타임에 <c>AddComponent&lt;TextMeshPro&gt;()</c>로 새로 만드는 버튼 라벨 — 는
    ///    TMP Settings의 전역 기본 폰트(LiberationSans SDF, 한글 미포함)로 떨어진다. 한글을 그리려면
    ///    전역 Fallback Font Asset(AppleGothic SDF)을 거쳐야 하는데, 이때 원본 폰트와 다른 머티리얼이
    ///    필요해서 TMP가 자동으로 별도의 SubMesh 자식 오브젝트를 만든다.
    /// 2) 그 SubMesh는 원본 텍스트의 MeshRenderer와 별개의 GameObject/MeshRenderer이므로, 우리가 원본
    ///    렌더러에만 수동으로 지정하는 sortingLayer/sortingOrder를 물려받지 못하고 기본값(Default
    ///    레이어, order 0)으로 남는다 — 배경이나 다른 Bubble과 뒤섞여서 렌더링 순서가 꼬이는 원인.
    ///
    /// 이 유틸은 (1) 폰트를 명시적으로 지정해 애초에 SubMesh가 생기지 않게 하고, (2) 그래도 생긴
    /// SubMesh가 있다면 원본 렌더러와 동일한 sortingLayer/sortingOrder로 강제 동기화한다.
    /// 새 텍스트가 생기는 지점(동적 버튼 생성, 대사 표시 등) 어디서든 호출하면 된다.
    /// </summary>
    public static class TMPKoreanFix
    {
        /// <summary>
        /// text의 폰트를 fontAsset으로 고정하고, text 및 그 SubMesh 전체의 sortingLayer/Order를
        /// text 자신의 primary MeshRenderer 기준으로 맞춘다. fontAsset이 null이면 폰트는 건드리지
        /// 않고 sortingLayer/Order 동기화만 수행한다.
        /// </summary>
        public static void Apply(TMP_Text text, TMP_FontAsset fontAsset, int sortingOrder)
        {
            if (text == null) return;

            if (fontAsset != null && text.font != fontAsset)
            {
                text.font = fontAsset;
            }

            // SubMesh는 실제로 mesh가 생성된 뒤에만 자식으로 존재한다. text를 막 세팅한 직후에는
            // 아직 mesh 갱신이 지연(lazy)되어 있을 수 있으므로 강제로 한 번 갱신시킨다.
            text.ForceMeshUpdate();

            var primary = text.GetComponent<MeshRenderer>();
            int layerId = primary != null ? primary.sortingLayerID : 0;

            var renderers = text.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingLayerID = layerId;
                renderers[i].sortingOrder = sortingOrder;
            }
        }
    }
}
