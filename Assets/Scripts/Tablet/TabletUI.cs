using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 태블릿 내부 UI 관리.
    /// 메인 메뉴 / 예약 목록 / 객실 관리 등 화면 전환을 담당한다.
    /// Panel 오브젝트 하위에 각 화면(Page)을 배치하고, 활성/비활성으로 전환.
    /// </summary>
    public class TabletUI : MonoBehaviour
    {
        // ────────────────────────────────────────
        //  화면(Page) 정의
        // ────────────────────────────────────────

        public enum Page
        {
            ReservationList,
            RoomManagement,
            Guidebook,
            Settings
        }

        // ────────────────────────────────────────
        //  Inspector 참조
        // ────────────────────────────────────────

        [Header("페이지 루트 오브젝트")]
        [SerializeField] private GameObject reservationPage;
        [SerializeField] private GameObject roomManagementPage;
        [SerializeField] private GameObject guidebookPage;
        [SerializeField] private GameObject settingsPage;

        [Header("예약 목록 UI")]
        [SerializeField] private Transform reservationListContainer;
        [SerializeField] private Sprite entryBgSprite;

        [Header("예약 목록 레이아웃")]
        [SerializeField] private float entryWidth = 12f;
        [SerializeField] private float entryHeight = 1.0f;
        [SerializeField] private float entrySpacing = 1.2f;
        [SerializeField] private float entryFontSize = 2.5f;
        [SerializeField] private float listStartY = 2.5f;

        [Header("Sorting")]
        [SerializeField] private int entrySortOrder = 52;
        [SerializeField] private int textSortOrder = 53;

        [Header("색상")]
        [SerializeField] private Color entryBgColor = new Color(1f, 0.98f, 0.92f, 1f);
        [SerializeField] private Color reservedColor = new Color(0.2f, 0.6f, 0.3f, 1f);
        [SerializeField] private Color walkInColor = new Color(0.7f, 0.3f, 0.2f, 1f);
        [SerializeField] private Color textColor = new Color(0.15f, 0.1f, 0.1f, 1f);

        [Header("데이터")]
        [SerializeField] private TabletController tabletController;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private RoomUI roomUI;

        // ────────────────────────────────────────
        //  내부 상태
        // ────────────────────────────────────────

        private Page _currentPage;
        private readonly List<GameObject> _spawnedEntries = new List<GameObject>();
        private Camera _cachedCam;

        // ────────────────────────────────────────
        //  초기화
        // ────────────────────────────────────────

        private void OnEnable()
        {
            HideAllPages();
        }

        private void OnDisable()
        {
            HideAllPages();
        }


        // ────────────────────────────────────────
        //  페이지 전환
        // ────────────────────────────────────────

        /// <summary>지정한 페이지로 전환</summary>
        public void ShowPage(Page page)
        {
            _currentPage = page;
            HideAllPages();

            switch (page)
            {
                case Page.ReservationList:
                    SetPageActive(reservationPage, true);
                    break;

                case Page.RoomManagement:
                    SetPageActive(roomManagementPage, true);
                    if (roomUI != null) roomUI.OnPageOpened();
                    break;

                case Page.Guidebook:
                    SetPageActive(guidebookPage, true);
                    break;

                case Page.Settings:
                    SetPageActive(settingsPage, true);
                    break;
            }
        }

        /// <summary>메인 메뉴로 돌아가기 (뒤로가기 버튼용)</summary>
        public void GoBack()
        {
            tabletController.Close();
        }

        // ────────────────────────────────────────
        //  메인 메뉴 버튼 콜백 (Update에서 클릭 감지)
        // ────────────────────────────────────────

        /// <summary>메인 메뉴에서 버튼 이름으로 페이지 전환</summary>
        public void OnMenuButtonClicked(string buttonName)
        {
            switch (buttonName)
            {
                case "reservation":
                    ShowPage(Page.ReservationList);
                    break;
                case "room":
                    ShowPage(Page.RoomManagement);
                    break;
                case "guidebook":
                    ShowPage(Page.Guidebook);
                    break;
                case "settings":
                    ShowPage(Page.Settings);
                    break;
            }
        }

        // ────────────────────────────────────────
        //  예약 목록 생성
        // ────────────────────────────────────────

        private void RefreshReservationList()
        {
            ClearEntries();

            if (dayManager == null || dayManager.TodaysGuests == null) return;

            var guests = dayManager.TodaysGuests;
            for (int i = 0; i < guests.Count; i++)
            {
                float y = listStartY - i * entrySpacing;
                _spawnedEntries.Add(CreateGuestEntry(guests[i], i, y));
            }
        }

        /// <summary>
        /// 예약 목록 항목 하나 생성.
        /// [상태] 이름 (종) | 식성 | 활동시간
        /// </summary>
        private GameObject CreateGuestEntry(Animal guest, int index, float localY)
        {
            var parent = reservationListContainer != null
                ? reservationListContainer
                : reservationPage.transform;

            // 항목 루트
            var entry = new GameObject("Entry_" + index);
            entry.transform.SetParent(parent);
            entry.transform.localPosition = new Vector3(0f, localY, 0f);
            entry.transform.localScale = Vector3.one;

            // 배경
            var bg = new GameObject("Bg");
            bg.transform.SetParent(entry.transform);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(entryWidth, entryHeight, 1f);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = entryBgSprite;
            bgSr.color = entryBgColor;
            bgSr.sortingOrder = entrySortOrder;

            // 예약 상태 표시
            bool isReserved = guest.hasReservation;
            string statusText = isReserved ? "예약" : "워크인";
            Color statusColor = isReserved ? reservedColor : walkInColor;

            // 텍스트: [상태] 이름 (종) | 식성 | 활동
            string dietText = guest.species.dietType == DietType.Herbivore ? "초식" : "육식";
            string activityText = guest.species.activityCycle == ActivityCycle.Diurnal ? "주행성" : "야행성";

            string displayText = string.Format(
                "<color=#{0}>[{1}]</color>  {2} ({3})  |  {4}  |  {5}",
                ColorUtility.ToHtmlStringRGB(statusColor),
                statusText,
                guest.guestName,
                guest.species.displayName,
                dietText,
                activityText
            );

            // 특이사항 추가
            if (guest.species.leavesOdour)
                displayText += "  | <color=#B8860B>냄새</color>";
            if (guest.species.causesDamage)
                displayText += "  | <color=#CD5C5C>파손</color>";

            // 텍스트 오브젝트
            var label = new GameObject("Label");
            label.transform.SetParent(entry.transform);
            label.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            label.transform.localScale = Vector3.one;

            var tmp = label.AddComponent<TextMeshPro>();
            tmp.text = displayText;
            tmp.fontSize = entryFontSize;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = textColor;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(entryWidth * 0.95f, entryHeight);
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var mr = label.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = textSortOrder;

            return entry;
        }

        // ────────────────────────────────────────
        //  정리
        // ────────────────────────────────────────

        private void ClearEntries()
        {
            foreach (var go in _spawnedEntries)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _spawnedEntries.Clear();
        }

        private void SetPageActive(GameObject page, bool active)
        {
            if (page != null) page.SetActive(active);
        }


        private void HideAllPages()
        {
            SetPageActive(reservationPage, false);
            SetPageActive(roomManagementPage, false);
            SetPageActive(guidebookPage, false);
            SetPageActive(settingsPage, false);
        }


        public bool CanUseMainMenuButtons()
        {
            return isActiveAndEnabled && !HasOpenPage();
        }

        private bool HasOpenPage()
        {
            return IsPageActive(reservationPage)
                || IsPageActive(roomManagementPage)
                || IsPageActive(guidebookPage)
                || IsPageActive(settingsPage);
        }

        private static bool IsPageActive(GameObject page)
        {
            return page != null && page.activeInHierarchy;
        }
    }
}
