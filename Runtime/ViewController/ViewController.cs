using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace MacacaGames.ViewSystem
{
    public class ViewController : ViewControllerBase
    {
        public static ViewController Instance;
        public bool IsReady = false;
        public static ViewElementRuntimePool runtimePool;
        public ViewElementPool viewElementPool;
        static float maxClampTime = 1;
        [SerializeField] public bool initOnAwake = true;
        [SerializeField] public bool autoPrewarm = true;
        [SerializeField] private ViewSystemSaveData viewSystemSaveData;

        /// <summary>
        /// Set this delegate before Init() to enable lazy Addressable loading of ViewElement prefabs.
        /// If null, the system falls back to direct prefab references.
        /// </summary>
        public static ViewElementLoadDelegate LoadViewElementAsync;

        /// <summary>
        /// Optional: release a previously loaded ViewElement prefab by address.
        /// </summary>
        public static ViewElementReleaseDelegate ReleaseViewElement;

        /// <summary>
        /// Whether Addressable lazy loading is both enabled in settings AND delegate is wired.
        /// </summary>
        private bool UseAddressableLoading =>
            viewSystemSaveData != null &&
            viewSystemSaveData.globalSetting.useAddressableLoading &&
            LoadViewElementAsync != null;

        private bool _isAsyncPrewarmRunning = false;

        Transform transformCache;
        Transform rootCanvasTransform;

        private Transform pageRootTransform;

        // Add a field to store all Canvas transforms
        private static List<Transform> _childCanvasTransforms = new List<Transform>();

        public override Canvas GetCanvas()
        {
            return rootCanvasTransform.GetComponent<Canvas>();
        }

        public Transform GetPageRootTransform()
        {
            return pageRootTransform;
        }

        // Use this for initialization
        protected override void Awake()
        {
            transformCache = transform;
            base.Awake();
            _incance = Instance = this;
            ViewRuntimeOverride.ClearCachedEventDelegate();
            if (initOnAwake)
            {
                Init();
            }
        }
        
        /// <summary>
        /// Dynamic load view system data
        /// </summary>
        /// <param name="viewSystemSaveData"></param>
        public void SetSaveDataManually(ViewSystemSaveData viewSystemSaveData)
        {
            if (viewSystemSaveData == null)
            {
                ViewSystemLog.LogError("SetSaveDataManually called with null save data.");
                return;
            }

            this.viewSystemSaveData = viewSystemSaveData;
            Init();
        }

        public void Init()
        {
            // Return if is already init
            if (IsReady)
            {
                return;
            }

            //Create ViewElementPool
            if (gameObject.name != viewSystemSaveData.globalSetting.ViewControllerObjectPath)
            {
                ViewSystemLog.LogWarning(
                    "The GameObject which attached ViewController is not match the setting in Base Setting.");
            }

            //Create UIRoot
            var uiRoot = Instantiate(viewSystemSaveData.globalSetting.UIRoot).transform;
            uiRoot.SetParent(transformCache);
            uiRoot.localPosition = viewSystemSaveData.globalSetting.UIRoot.transform.localPosition;
            uiRoot.gameObject.name = viewSystemSaveData.globalSetting.UIRoot.name;

            
            _childCanvasTransforms = uiRoot.GetComponentsInChildren<Canvas>().Select(canvas => canvas.transform).ToList();
            rootCanvasTransform = _childCanvasTransforms[0];

            if (!string.IsNullOrEmpty(viewSystemSaveData.globalSetting.customPageRootPath))
            {
                var target = rootCanvasTransform.Find(viewSystemSaveData.globalSetting.customPageRootPath);
                if (target == null)
                {
                    pageRootTransform = rootCanvasTransform;
                    ViewSystemLog.LogWarning("Custom Page Root Path is set but not found, use Canvas as Page Root.");
                }
                else
                {
                    pageRootTransform = target;
                }
            }
            else
            {
                pageRootTransform = rootCanvasTransform;
                ViewSystemLog.LogWarning("Custom Page Root Path not set use Canvas as Page Root.");
            }

            var go = new GameObject("ViewElementPool");
            go.transform.SetParent(transformCache);
            go.AddComponent<RectTransform>();
            viewElementPool = go.AddComponent<ViewElementPool>();

            runtimePool = gameObject.AddComponent<ViewElementRuntimePool>();
            runtimePool.Init(viewElementPool);

            ViewElement.runtimePool = runtimePool;
            ViewElement.viewElementPool = viewElementPool;
            SingletonViewElementDictionary = new Dictionary<System.Type, Component>();
            sharedViewElementModel = new Dictionary<Type, object>();
            maxClampTime = viewSystemSaveData.globalSetting.MaxWaitingTime;
            minimumTimeInterval = viewSystemSaveData.globalSetting.minimumTimeInterval;
            builtInClickProtection = viewSystemSaveData.globalSetting.builtInClickProtection;
            try
            {
                breakPointsStatus = viewSystemSaveData.globalSetting.breakPoints.ToDictionary(m => m, m => false);
            }
            catch (Exception ex)
            {
                ViewSystemLog.LogError($"Error occur while proccess breakpoint {ex.Message}");
            }


            viewStates = viewSystemSaveData.GetViewStateSaveDatas().Select(m => m.viewState)
                .ToDictionary(m => m.name, m => m);
            viewPages = viewSystemSaveData.GetViewPageSaveDatas().Select(m => m.viewPage)
                .ToDictionary(m => m.name, m => m);
            viewStatesNames = viewStates.Values.Select(m => m.name);

            if (autoPrewarm)
            {
                PrewarmSingletonViewElement();
            }

            // If async prewarm is running, IsReady will be set after it completes.
            // Otherwise set it immediately.
            if (!_isAsyncPrewarmRunning)
            {
                IsReady = true;
            }
        }

        IEnumerator FixedTimeRecovery()
        {
            while (true)
            {
                yield return Yielders.GetWaitForSeconds(2);
                if (IsPageTransition || IsOverlayTransition)
                {
                    continue;
                }

                yield return runtimePool.RecoveryQueuedViewElement();
            }
        }

        protected override void Start()
        {
            //Load ViewPages and ViewStates from ViewSystemSaveData
            base.Start();
        }

        void OnDestroy()
        {
            ViewSystemUtilitys.ClearRectTransformCache();
        }

        #region Injection and ViewElementSingleton

        static Dictionary<System.Type, Component> SingletonViewElementDictionary;

        [System.Obsolete("GetInjectionInstance is obsolete, use GetSingletonViewElement or GetSingletonViewElementAsync instead")]
        public T GetInjectionInstance<T>() where T : Component, IViewElementSingleton
        {
            return GetSingletonViewElement<T>();
        }

        public T GetSingletonViewElement<T>() where T : Component, IViewElementSingleton
        {
            if (SingletonViewElementDictionary.TryGetValue(typeof(T), out Component result))
            {
                return (T)result;
            }
            else
            {
                IViewElementSingleton s = WarmupUniqueViewElement(typeof(T));
                if (s != null)
                {
                    return (T)s;
                }

                ViewSystemLog.LogError(
                    "Target type cannot been found, are you sure your ViewElement which attach target Component is unique? " +
                    "If using Addressable loading, use GetSingletonViewElementAsync<T>() instead.");
            }

            return null;
        }

        /// <summary>
        /// Async version of GetSingletonViewElement. Supports on-demand Addressable loading.
        /// Use this when Addressable lazy loading is enabled.
        /// </summary>
        public async Task<T> GetSingletonViewElementAsync<T>() where T : Component, IViewElementSingleton
        {
            // Fast path: already in dictionary
            if (SingletonViewElementDictionary.TryGetValue(typeof(T), out Component result))
            {
                return (T)result;
            }

            // Try sync warmup first (works for direct refs or already-cached assets)
            IViewElementSingleton s = WarmupUniqueViewElement(typeof(T));
            if (s != null)
            {
                return (T)s;
            }

            // Async path: load via Addressables and wait
            if (UseAddressableLoading)
            {
                var item = viewSystemSaveData.uniqueViewElementTable.FirstOrDefault(m => m.type == typeof(T).ToString());
                if (item != null && !string.IsNullOrEmpty(item.viewElementAddress))
                {
                    ViewSystemLog.Log($"Async loading singleton ViewElement {typeof(T).Name} at address: {item.viewElementAddress}");
                    try
                    {
                        var go = await LoadViewElementAsync(item.viewElementAddress);
                        if (go != null)
                        {
                            item.loadedViewElementGameObject = go;
                            var warmupResult = RegisterUniqueViewElement(go, typeof(T));
                            if (warmupResult != null)
                            {
                                return (T)warmupResult;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ViewSystemLog.LogError($"Failed async load for singleton {typeof(T).Name}: {ex.Message}");
                    }
                }
            }

            ViewSystemLog.LogError(
                $"Target type {typeof(T).Name} cannot been found even after async load attempt.");
            return null;
        }

        IViewElementSingleton WarmupUniqueViewElement(Type type)
        {
            var item = viewSystemSaveData.uniqueViewElementTable.FirstOrDefault(m => m.type == type.ToString());
            if (item == null)
            {
                ViewSystemLog.Log("Cannot found matched type in the uniqueViewElementTable");
                return null;
            }

            // Use ResolvedGameObject which checks direct ref first, then async-loaded cache
            var sourceGo = item.ResolvedGameObject;

            // Fallback: try loading from Addressable delegate (returns synchronously if already cached)
            if (sourceGo == null && !string.IsNullOrEmpty(item.viewElementAddress) && UseAddressableLoading)
            {
                try
                {
                    var task = LoadViewElementAsync(item.viewElementAddress);
                    if (task.IsCompleted && !task.IsFaulted)
                    {
                        sourceGo = task.Result;
                        item.loadedViewElementGameObject = sourceGo;
                    }
                }
                catch (Exception ex)
                {
                    ViewSystemLog.LogError($"Failed on-demand load for {type}: {ex.Message}");
                }
            }

            if (sourceGo == null)
            {
                return null;
            }

            return RegisterUniqueViewElement(sourceGo, type);
        }

        /// <summary>
        /// Instantiate a unique ViewElement from a source prefab and register all IViewElementSingleton components.
        /// </summary>
        IViewElementSingleton RegisterUniqueViewElement(GameObject sourceGo, Type targetType)
        {
            IViewElementSingleton result = null;
            var r = runtimePool.PrewarmUniqueViewElement(sourceGo.GetComponent<ViewElement>());
            if (r != null)
            {
                foreach (var i in r.GetComponents<IViewElementSingleton>())
                {
                    if (i.GetType() == targetType)
                    {
                        result = i;
                    }

                    var c = (Component)i;
                    var t = c.GetType();
                    if (!SingletonViewElementDictionary.ContainsKey(t))
                        SingletonViewElementDictionary.Add(t, c);
                    else
                    {
                        ViewSystemLog.LogWarning("Type " + t + " has been injected");
                    }
                }
            }

            return result;
        }

        void PrewarmSingletonViewElement()
        {
            // Collect all ViewPageItems that have direct prefab references and are unique (sync path)
            var allItems = viewStates.Values.Select(m => m.viewPageItems).SelectMany(ma => ma)
                .Concat(viewPages.Values.Select(m => m.viewPageItems).SelectMany(ma => ma));

            foreach (var pageItem in allItems)
            {
                if (pageItem.viewElement == null || !pageItem.viewElement.IsUnique)
                    continue;

                PrewarmSingleViewElement(pageItem.viewElement);
            }

            // Sync prewarm for uniqueViewElementTable entries with direct references
            foreach (var tableItem in viewSystemSaveData.uniqueViewElementTable)
            {
                if (tableItem.viewElementGameObject != null)
                {
                    var ve = tableItem.viewElementGameObject.GetComponent<ViewElement>();
                    if (ve != null && ve.IsUnique)
                    {
                        PrewarmSingleViewElement(ve);
                    }
                }
            }

            // For items that need async loading, start a coroutine
            if (UseAddressableLoading)
            {
                var asyncPageItems = allItems.Where(m => m.NeedsAsyncLoad).ToList();
                var asyncUniqueItems = viewSystemSaveData.uniqueViewElementTable
                    .Where(m => m.NeedsAsyncLoad).ToList();

                ViewSystemLog.Log($"Async prewarm: {asyncPageItems.Count} page items, {asyncUniqueItems.Count} unique items to load.");
                foreach (var u in asyncUniqueItems)
                {
                    ViewSystemLog.Log($"  Unique item to load: type={u.type}, address={u.viewElementAddress}");
                }

                if (asyncPageItems.Count > 0 || asyncUniqueItems.Count > 0)
                {
                    _isAsyncPrewarmRunning = true;
                    StartCoroutine(PrewarmSingletonViewElementAsync(asyncPageItems, asyncUniqueItems));
                }
                else
                {
                    ViewSystemLog.Log("No async items to prewarm.");
                }
            }
        }

        IEnumerator PrewarmSingletonViewElementAsync(List<ViewPageItem> pageItems, List<UniqueViewElementTableData> uniqueItems)
        {
            var loadTasks = new List<Task>();

            // Load ViewPageItems via Addressables
            foreach (var item in pageItems)
            {
                loadTasks.Add(LoadViewElementForItem(item));
            }

            // Load UniqueViewElementTable items via Addressables
            foreach (var item in uniqueItems)
            {
                loadTasks.Add(LoadUniqueViewElementAsync(item));
            }

            var allTask = Task.WhenAll(loadTasks);
            while (!allTask.IsCompleted)
                yield return null;

            if (allTask.IsFaulted)
            {
                ViewSystemLog.LogError($"Async prewarm had errors: {allTask.Exception?.Message}");
            }

            // Prewarm unique ViewElements from ViewPageItems
            foreach (var item in pageItems)
            {
                if (item.viewElement != null && item.viewElement.IsUnique)
                {
                    PrewarmSingleViewElement(item.viewElement);
                }
            }

            // Prewarm unique ViewElements from uniqueViewElementTable
            foreach (var item in uniqueItems)
            {
                var go = item.ResolvedGameObject;
                if (go != null)
                {
                    var ve = go.GetComponent<ViewElement>();
                    if (ve != null && ve.IsUnique)
                    {
                        ViewSystemLog.Log($"Prewarming unique element: {ve.name}");
                        PrewarmSingleViewElement(ve);
                    }
                    else
                    {
                        ViewSystemLog.LogWarning($"Loaded unique item at address {item.viewElementAddress} but it has no ViewElement or IsUnique=false");
                    }
                }
                else
                {
                    ViewSystemLog.LogError($"Unique item at address {item.viewElementAddress} (type: {item.type}) still null after load");
                }
            }

            _isAsyncPrewarmRunning = false;
            IsReady = true;
            ViewSystemLog.Log($"Async prewarm complete. SingletonViewElementDictionary has {SingletonViewElementDictionary.Count} entries. ViewController is ready.");
        }

        async Task LoadUniqueViewElementAsync(UniqueViewElementTableData item)
        {
            try
            {
                var go = await LoadViewElementAsync(item.viewElementAddress).ConfigureAwait(false);
                if (go != null)
                {
                    item.loadedViewElementGameObject = go;
                }
                else
                {
                    ViewSystemLog.LogError($"LoadViewElementAsync returned null for unique element address: {item.viewElementAddress}");
                }
            }
            catch (Exception ex)
            {
                ViewSystemLog.LogError($"Failed to load unique ViewElement at address '{item.viewElementAddress}': {ex.Message}");
            }
        }

        void PrewarmSingleViewElement(ViewElement item)
        {
            if (item == null || !item.IsUnique) return;

            var r = runtimePool.PrewarmUniqueViewElement(item);
            if (r != null)
            {
                foreach (var i in r.GetComponents<IViewElementSingleton>())
                {
                    var c = (Component)i;
                    var t = c.GetType();
                    if (!SingletonViewElementDictionary.ContainsKey(t))
                        SingletonViewElementDictionary.Add(t, c);
                    else
                        ViewSystemLog.LogWarning("Type " + t + " has been injected");
                }
            }
        }

        static Dictionary<Type, object> sharedViewElementModel = new Dictionary<Type, object>();
        static object[] pageModelsCache = null;

        /// <summary>
        /// Set the model data to the System, it will become a Shared Model
        /// Each type can only have one value/instance, the system will automatically override the new value if duplicate type is trying to Set
        /// </summary>
        /// <param name="models"></param>
        public void SetSharedModels(params object[] models)
        {
            foreach (var item in models)
            {
                var type = item.GetType();
                if (SingletonViewElementDictionary.ContainsKey(type))
                {
                    ViewSystemLog.LogWarning(
                        $"{type.ToString()} is SingletonViewElement no require to set from this API");
                    continue;
                }

                if (sharedViewElementModel.ContainsKey(type))
                {
                    ViewSystemLog.LogWarning($"{type.ToString()} is already in set before, will replace to new value");
                    sharedViewElementModel[type] = item;
                    continue;
                }

                sharedViewElementModel.TryAdd(type, item);
            }
        }

        internal static void InjectModels(object targetObject)
        {
            Type contract = targetObject.GetType();

            IEnumerable<MemberInfo> members =
                contract.FindMembers(
                    MemberTypes.Property | MemberTypes.Field,
                    BindingFlags.FlattenHierarchy | BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.Static,
                    (m, i) => m.GetCustomAttribute(typeof(ViewElementInjectAttribute), true) != null,
                    null);

            var groupedMember = members.GroupBy(m => m.GetMemberType());
            foreach (var gp in groupedMember)
            {
                var isMultiple = gp.Count() > 1;
                foreach (var info in gp)
                {
                    var target = GetModelInstance(info,
                        info.GetCustomAttribute<ViewElementInjectAttribute>().injectScope, isMultiple);
                    if (target != null)
                    {
                        info.SetValue(targetObject, target);
                    }
                }
            }
        }

        internal static object GetModelInstance(MemberInfo memberInfo, InjectScope injectScope, bool isMultiple = false)
        {
            Type typeToSearch = memberInfo.GetMemberType();
            return GetModelInstance(typeToSearch, memberInfo.Name, injectScope, isMultiple);
        }

        internal static object GetModelInstance(Type typeToSearch, string memberNameKey, InjectScope injectScope,
            bool isMultiple = false)
        {
            switch (injectScope)
            {
                case InjectScope.PageOnly:
                    return SearchInModels(typeToSearch, memberNameKey, isMultiple);
                case InjectScope.SharedOnly:
                    return SearchInSharedModels(typeToSearch) ?? SearchInSingletonModels(typeToSearch);
                case InjectScope.PageFirst:
                    return SearchInModels(typeToSearch, memberNameKey, isMultiple) ??
                           SearchInSharedModels(typeToSearch) ?? SearchInSingletonModels(typeToSearch);
                case InjectScope.SharedFirst:
                    return SearchInSharedModels(typeToSearch) ?? SearchInSingletonModels(typeToSearch) ??
                        SearchInModels(typeToSearch, memberNameKey, isMultiple);
                default:
                    throw new ArgumentException("Invalid scope");
            }
        }

        static object SearchInModels(Type typeToSearch, string memberNameKey, bool tryDictionary = false)
        {
            var models = pageModelsCache;
            if (models == null || models.Length == 0)
            {
                return null;
            }

            if (tryDictionary)
            {
                if (string.IsNullOrEmpty(memberNameKey))
                {
                    throw new InvalidOperationException(
                        "If try search ViewInjectDictionary, the memberNameKey is required");
                }

                Type genericClass = typeof(ViewInjectDictionary<>);
                Type constructedClass = genericClass.MakeGenericType(typeToSearch);

                var obj = models.SingleOrDefault(m => m.GetType() == constructedClass);

                if (obj == null)
                {
                    goto DefaultSearch;
                }

                var dictionary = obj as ViewInjectDictionary;
                if (dictionary.ContainsKey(memberNameKey))
                {
                    return dictionary.GetValue(memberNameKey);
                }

                goto DefaultSearch;
            }

            DefaultSearch:
            try
            {
                return models.SingleOrDefault(model => model.GetType() == typeToSearch);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "When using ViewSystem model biding, each Type only available for one instance, if you would like to bind multiple instance of a Type use Collections(List, Array) or ViewInjectDictionary<T> instead.");
            }
        }

        static object SearchInSharedModels(Type typeToSearch)
        {
            return sharedViewElementModel.TryGetValue(typeToSearch, out object value) ? value : null;
        }

        static object SearchInSingletonModels(Type typeToSearch)
        {
            return SingletonViewElementDictionary.TryGetValue(typeToSearch, out Component value) ? value : null;
        }

        #endregion

        IEnumerable<ViewPageItem> PrepareRuntimeReference(IEnumerable<ViewPageItem> viewPageItems)
        {
            foreach (var item in viewPageItems)
            {
                if (item.viewElement != null)
                {
                    item.runtimeViewElement = runtimePool.RequestViewElement(item.viewElement);
                }
                else
                {
                    ViewSystemLog.LogError(
                        $"The viewElement in ViewPageItem : {item.Id} is null or missing, that is all we know, please check the page you're trying to change to.");
                }
            }

            return viewPageItems;
        }

        /// <summary>
        /// Async version: loads prefabs via LoadViewElementAsync delegate first, then requests from pool.
        /// </summary>
        IEnumerator PrepareRuntimeReferenceAsync(IEnumerable<ViewPageItem> viewPageItems, Action<List<ViewPageItem>> onComplete)
        {
            var itemList = viewPageItems.ToList();

            // Phase 1: Kick off all async loads in parallel
            var loadTasks = new List<Task>();
            foreach (var item in itemList)
            {
                if (item.NeedsAsyncLoad && UseAddressableLoading)
                {
                    loadTasks.Add(LoadViewElementForItem(item));
                }
            }

            // Phase 2: Busy-wait for all loads to complete
            if (loadTasks.Count > 0)
            {
                var allTask = Task.WhenAll(loadTasks);
                while (!allTask.IsCompleted)
                {
                    yield return null;
                }

                if (allTask.IsFaulted)
                {
                    ViewSystemLog.LogError($"Failed to load some ViewElement prefabs: {allTask.Exception?.Message}");
                }
            }

            // Phase 3: Now all prefabs are loaded, do synchronous pool requests
            foreach (var item in itemList)
            {
                if (item.viewElement != null)
                {
                    item.runtimeViewElement = runtimePool.RequestViewElement(item.viewElement);
                }
                else
                {
                    ViewSystemLog.LogError(
                        $"The viewElement in ViewPageItem : {item.Id} (address: {item.viewElementAddress}) is null or missing after load attempt.");
                }
            }

            onComplete?.Invoke(itemList);
        }

        /// <summary>
        /// Loads a single ViewPageItem's prefab via the delegate.
        /// </summary>
        async Task LoadViewElementForItem(ViewPageItem item)
        {
            try
            {
                var go = await LoadViewElementAsync(item.viewElementAddress).ConfigureAwait(false);
                if (go != null)
                {
                    item.loadedViewElementObject = go;
                }
                else
                {
                    ViewSystemLog.LogError($"LoadViewElementAsync returned null for address: {item.viewElementAddress}");
                }
            }
            catch (Exception ex)
            {
                ViewSystemLog.LogError($"Failed to load ViewElement at address '{item.viewElementAddress}': {ex.Message}");
            }
        }

        private float nextViewPageWaitTime = 0;

        List<ViewElement> tempCurrentLiveElements = new List<ViewElement>();

        
        protected new List<ViewElement> currentLiveElements
        {
            get
            {
                tempCurrentLiveElements.Clear();
                tempCurrentLiveElements.AddRange(currentLiveElementsInViewPage);
                tempCurrentLiveElements.AddRange(currentLiveElementsInViewState);
                return tempCurrentLiveElements;
            }
        }


        [ReadOnly, SerializeField] protected List<ViewElement> currentLiveElementsInViewPage = new List<ViewElement>();
        [ReadOnly, SerializeField] protected List<ViewElement> currentLiveElementsInViewState = new List<ViewElement>();

        public override IEnumerator ChangePageBase(string viewPageName, Action OnStart, Action OnChanged,
            Action OnComplete, bool ignoreTimeScale, bool ignoreClickProtection, params object[] models)
        {
            if (IsOverPageLive(viewPageName))
            {
                ViewSystemLog.LogError(
                    "The target FullPage is shown as Overlay page, there is not allow to shown as FullPage before it is as Overlay mode, Leave the Page first then change to FullPage.");
                ChangePageToCoroutine = null;
                yield break;
            }

            ViewSystemLog.Log($"ChangePage Invoke {viewPageName}");
            //Get ViewPage object
            ViewPage nextViewPageForCurrentChangePage = null;

            //Not found
            if (!viewPages.TryGetValue(viewPageName, out ViewPage _nextViewPage))
            {
                ViewSystemLog.LogError("No view page match " + viewPageName + " Found");
                ChangePageToCoroutine = null;
                yield break;
            }

            nextViewPage = _nextViewPage;
            nextViewPageForCurrentChangePage = nextViewPage;

            if (nextViewPageForCurrentChangePage.viewPageType == ViewPage.ViewPageType.Overlay)
            {
                ViewSystemLog.LogWarning(
                    "To shown Page is an Overlay ViewPage use ShowOverlayViewPage() instead method \n current version will redirect to this method automatically, but this behaviour may be changed in future release.");
                ShowOverlayViewPageBase(nextViewPageForCurrentChangePage, true, OnStart, OnChanged, OnComplete,
                    ignoreTimeScale, ignoreClickProtection, null, false, null, null);
                ChangePageToCoroutine = null;
                yield break;
            }

            //Prepare runtime page root
            string viewPageRootName = ViewSystemUtilitys.GetPageRootName(nextViewPageForCurrentChangePage);
            var pageWrapper = ViewSystemUtilitys.CreatePageTransform(viewPageRootName, pageRootTransform,
                nextViewPageForCurrentChangePage.canvasSortOrder,
                viewSystemSaveData.globalSetting.UIPageTransformLayerName);
            nextViewPageForCurrentChangePage.runtimePageRoot = pageWrapper.rectTransform;

            pageWrapper.safePadding.SetPaddingValue(GetSafePaddingSetting(nextViewPageForCurrentChangePage));

            // pageWrapper.safePadding.SetPaddingValue(nextViewPageForCurrentChangePage.edgeValues);

            //All checks passed, start page transition
            //IsPageTransition = true;

            nextViewState = null;
            viewStates.TryGetValue(nextViewPageForCurrentChangePage.viewState, out ViewState _nextViewState);
            nextViewState = _nextViewState;

            var pageItems = GetAllViewPageItemInViewPage(nextViewPageForCurrentChangePage);
            IEnumerable<ViewPageItem> viewItemNextState = GetAllViewPageItemInViewState(nextViewState);
            List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem>();

            // Check if any items need async loading
            bool anyPageNeedsAsync = UseAddressableLoading && pageItems.Any(i => i.NeedsAsyncLoad);
            bool anyStateNeedsAsync = UseAddressableLoading &&
                _nextViewState != currentViewState &&
                viewItemNextState.Any(i => i.NeedsAsyncLoad);

            IEnumerable<ViewPageItem> viewItemNextPage;
            if (anyPageNeedsAsync || anyStateNeedsAsync)
            {
                // Async path: load page items
                List<ViewPageItem> loadedPageItems = null;
                yield return PrepareRuntimeReferenceAsync(pageItems, result => loadedPageItems = result);
                viewItemNextPage = loadedPageItems;

                // Async path: load state items if state changed
                if (_nextViewState != currentViewState)
                {
                    List<ViewPageItem> loadedStateItems = null;
                    yield return PrepareRuntimeReferenceAsync(viewItemNextState, result => loadedStateItems = result);
                    viewItemNextState = loadedStateItems;
                }
            }
            else
            {
                // Synchronous fallback (no addresses, or delegate not set)
                viewItemNextPage = PrepareRuntimeReference(pageItems);
                if (_nextViewState != currentViewState)
                {
                    viewItemNextState = PrepareRuntimeReference(viewItemNextState);
                }
            }

            // All reference preparing is done start do the stuff for change page
            InvokeOnViewPageChangeStart(this,
                new ViewPageTrisitionEventArgs(currentViewPage, nextViewPageForCurrentChangePage));
            OnStart?.Invoke();

            List<ViewElement> viewElementDoesExitsInNextPage = new List<ViewElement>();

            var allViewElementForNextPageInViewPage = viewItemNextPage.Select(m => m.runtimeViewElement).ToList();
            var allViewElementForNextPageInViewState = viewItemNextState.Select(m => m.runtimeViewElement).ToList();

            foreach (var item in currentLiveElementsInViewPage)
            {
                //If not present, add to the removal list
                if (allViewElementForNextPageInViewPage.Contains(item) == false &&
                    allViewElementForNextPageInViewState.Contains(item) == false)
                {
                    //Add to the removal list
                    viewElementDoesExitsInNextPage.Add(item);
                }
            }

            currentLiveElementsInViewPage.Clear();
            currentLiveElementsInViewPage = allViewElementForNextPageInViewPage;

            if (_nextViewState != currentViewState)
            {
                foreach (var item in currentLiveElementsInViewState)
                {
                    //If not present, add to the removal list
                    if (allViewElementForNextPageInViewState.Contains(item) == false &&
                        allViewElementForNextPageInViewPage.Contains(item) == false)
                    {
                        //Add to the removal list
                        viewElementDoesExitsInNextPage.Add(item);
                    }
                }

                currentLiveElementsInViewState.Clear();
                currentLiveElementsInViewState = allViewElementForNextPageInViewState;
            }

            //Trigger state change for leaving elements
            foreach (var item in viewElementDoesExitsInNextPage)
            {
                item.ChangePage(false, null, null, 0, 0);
            }

            float TimeForPerviousPageOnLeave = 0;
            switch (nextViewPageForCurrentChangePage.viewPageTransitionTimingType)
            {
                case ViewPage.ViewPageTransitionTimingType.AfterPervious:
                    //TimeForPerviousPageOnLeave = ViewSystemUtilitys.CalculateOnLeaveDuration(viewItemNextPage.Select(m => m.viewElement), maxClampTime);
                    TimeForPerviousPageOnLeave = nextViewPageWaitTime;
                    break;
                case ViewPage.ViewPageTransitionTimingType.WithPervious:
                    TimeForPerviousPageOnLeave = 0;
                    break;
                case ViewPage.ViewPageTransitionTimingType.Custom:
                    TimeForPerviousPageOnLeave = nextViewPageForCurrentChangePage.customPageTransitionWaitTime;
                    break;
            }

            //  nextViewPageForCurrentChangePageWaitTime = ViewSystemUtilitys.CalculateDelayOutTime(viewItemNextPage);
            nextViewPageWaitTime =
                ViewSystemUtilitys.CalculateOnLeaveDuration(viewItemNextPage.Select(m => m.viewElement), maxClampTime);

            //Wait for previous page's OnLeave to finish. Note: if the page has many Animators, this is an estimate and will be clamped to max waiting time
            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(TimeForPerviousPageOnLeave);
            else
                yield return Yielders.GetWaitForSeconds(TimeForPerviousPageOnLeave);

            viewItemForNextPage.AddRange(viewItemNextPage);
            if (viewItemNextState != null) viewItemForNextPage.AddRange(viewItemNextState);
            //Trigger state change for entering elements (ViewPage)
            foreach (var item in viewItemForNextPage.OrderBy(m => m.sortingOrder))
            {
                if (item.runtimeViewElement == null)
                {
                    ViewSystemLog.LogError($"The runtimeViewElement is null for some reason, ignore this item.");
                    continue;
                }

                //Apply models
                pageModelsCache = models;
                item.runtimeViewElement.ApplyModelInject();

                //Apply overrides
                item.runtimeViewElement.ApplyOverrides(item.overrideDatas);
                item.runtimeViewElement.ApplyEvents(item.eventDatas);

                var transformData = item.GetCurrentViewElementTransform(breakPointsStatus);

                if (!string.IsNullOrEmpty(transformData.parentPath))
                {
                    item.runtimeParent = transformCache.Find(transformData.parentPath);
                }
                else
                {
                    item.runtimeParent = nextViewPageForCurrentChangePage.runtimePageRoot;
                }

                item.runtimeViewElement.ChangePage(true, item.runtimeParent, transformData, item.sortingOrder,
                    item.TweenTime, item.delayIn);
            }

            foreach (var item in currentLiveElements.OrderBy(m => m.sortingOrder))
            {
                item.rectTransform.SetAsLastSibling();
            }

            float OnShowAnimationFinish =
                ViewSystemUtilitys.CalculateOnShowDuration(viewItemNextPage.Select(m => m.runtimeViewElement),
                    maxClampTime);

            //Update state
            UpdateCurrentViewStateAndNotifyEvent(nextViewPageForCurrentChangePage);
            foreach (var item in currentLiveElements)
            {
                item.OnChangedPage();
            }

            yield return runtimePool.RecoveryQueuedViewElement();

            OnChanged?.Invoke();

            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(OnShowAnimationFinish);
            else
                //Notify events
                yield return Yielders.GetWaitForSeconds(OnShowAnimationFinish);

            ChangePageToCoroutine = null;

            //Callback
            InvokeOnViewPageChangeEnd(this, new ViewPageEventArgs(nextViewPageForCurrentChangePage, lastViewPage));

            nextViewPageForCurrentChangePage = null;
            nextViewState = null;

            //2019.12.18 due to there may be new Callback be add, change the  OnComplete to all is done.
            OnComplete?.Invoke();
        }

        public override IEnumerator ShowOverlayViewPageBase(ViewPage vp, bool RePlayOnShowWhileSamePage, Action OnStart,
            Action OnChanged, Action OnComplete, bool ignoreTimeScale, bool ignoreClickProtection,
            RectTransform customRoot, bool createPageCanvas, int? order, params object[] models)
        {
            // Debug.Log("ShowOverlayViewPageBase " + vp.name);
            if (vp == null)
            {
                ViewSystemLog.Log("ViewPage is null");
                yield break;
            }

            // if (vp.viewPageType != ViewPage.ViewPageType.Overlay)
            // {
            //     ViewSystemLog.LogError("ViewPage " + vp.name + " is not an Overlay page");
            //     yield break;
            // }

            //Not using customRoot, Prepare runtime page root,
            if (customRoot == null || createPageCanvas)
            {
                string viewPageRootName =
                    ViewSystemUtilitys.GetPageRootName(vp, vp.viewPageType == ViewPage.ViewPageType.FullPage);
                var parent = customRoot == null ? pageRootTransform : customRoot;
                var orderValue = order.HasValue ? order.Value : vp.canvasSortOrder;

                var pageWrapper = ViewSystemUtilitys.CreatePageTransform(viewPageRootName, parent, orderValue,
                    viewSystemSaveData.globalSetting.UIPageTransformLayerName);
                pageWrapper.safePadding.SetPaddingValue(GetSafePaddingSetting(vp));

                if (customRoot != null || vp.runtimePageRoot == null)
                {
                    vp.runtimePageRoot = pageWrapper.rectTransform;
                }
            }
            else
            {
                vp.runtimePageRoot = customRoot;
            }

            ViewState viewState = null;
            viewStates.TryGetValue(vp.viewState, out viewState);


            List<ViewElement> viewElementDoesExitsInNextPage = new List<ViewElement>();
            IEnumerable<ViewPageItem> viewItemNextPage = null;
            IEnumerable<ViewPageItem> viewItemNextState = null;
            List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem>();

            string OverlayPageStateKey = GetOverlayStateKey(vp);
            bool samePage = false;
            //Check if an Overlay page with the same State is already on screen
            if (overlayPageStatusDict.TryGetValue(OverlayPageStateKey,
                    out ViewSystemUtilitys.OverlayPageStatus overlayPageStatus))
            {
                var overlayPageItems = GetAllViewPageItemInViewPage(vp);
                if (UseAddressableLoading && overlayPageItems.Any(i => i.NeedsAsyncLoad))
                {
                    List<ViewPageItem> loaded = null;
                    yield return PrepareRuntimeReferenceAsync(overlayPageItems, r => loaded = r);
                    viewItemNextPage = loaded;
                }
                else
                {
                    viewItemNextPage = PrepareRuntimeReference(overlayPageItems);
                }

                //Same OverlayState page is already on screen, remove different parts and show newly added parts
                if (!string.IsNullOrEmpty(vp.viewState))
                {
                    if (overlayPageStatus.viewPage.name != vp.name)
                    {
                        // Same State but different Page, find the diff
                        foreach (var item in overlayPageStatus.viewPage.viewPageItems)
                        {
                            if (!vp.viewPageItems.Select(m => m.runtimeViewElement).Contains(item.runtimeViewElement))
                                viewElementDoesExitsInNextPage.Add(item.runtimeViewElement);
                        }

                        overlayPageStatus.viewPage = vp;
                    }
                }
                else
                {
                    //If only ViewPage but still entered here, it means the page is still on screen
                    // RePlayOnShowWhileSamePage == false, update values and stop the old Coroutine
                    if (overlayPageStatus.pageChangeCoroutine != null)
                    {
                        StopCoroutine(overlayPageStatus.pageChangeCoroutine);
                    }

                    samePage = true;
                    overlayPageStatus.transition = ViewSystemUtilitys.OverlayPageStatus.Transition.Show;
                }
            }
            else
            {
                //Same OverlayState page is not on screen yet, create a new Status

                overlayPageStatus = new ViewSystemUtilitys.OverlayPageStatus();
                overlayPageStatus.viewPage = vp;
                overlayPageStatus.viewState = viewState;
                overlayPageStatus.transition = ViewSystemUtilitys.OverlayPageStatus.Transition.Show;

                var newOverlayPageItems = GetAllViewPageItemInViewPage(vp);
                if (UseAddressableLoading && newOverlayPageItems.Any(i => i.NeedsAsyncLoad))
                {
                    List<ViewPageItem> loaded = null;
                    yield return PrepareRuntimeReferenceAsync(newOverlayPageItems, r => loaded = r);
                    viewItemNextPage = loaded;
                }
                else
                {
                    viewItemNextPage = PrepareRuntimeReference(newOverlayPageItems);
                }

                // Pages without viewState don't need to process viewState's runtimeViewElement
                if (!string.IsNullOrEmpty(vp.viewState))
                {
                    // nextViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);
                    if (viewStates.TryGetValue(vp.viewState, out ViewState _nextViewState))
                    {
                        nextViewState = _nextViewState;
                        viewItemNextState = GetAllViewPageItemInViewState(nextViewState);
                        if (UseAddressableLoading && viewItemNextState.Any(i => i.NeedsAsyncLoad))
                        {
                            List<ViewPageItem> loadedState = null;
                            yield return PrepareRuntimeReferenceAsync(viewItemNextState, r => loadedState = r);
                            viewItemNextState = loadedState;
                        }
                        else
                        {
                            viewItemNextState = PrepareRuntimeReference(viewItemNextState);
                        }
                    }
                }

                overlayPageStatusDict[OverlayPageStateKey] = overlayPageStatus;
            }

            OnStart?.Invoke();

            if (viewItemNextState != null) viewItemForNextPage.AddRange(viewItemNextState);
            viewItemForNextPage.AddRange(viewItemNextPage);


            float onShowTime =
                ViewSystemUtilitys.CalculateOnShowDuration(viewItemNextPage.Select(m => m.runtimeViewElement));
            float onShowDelay = ViewSystemUtilitys.CalculateDelayInTime(viewItemNextPage);

            //Trigger state change for leaving elements
            foreach (var item in viewElementDoesExitsInNextPage)
            {
                // Debug.LogWarning($"{item.name} not exsit in next page");

                item.ChangePage(false, null, null, 0, 0, 0);
            }

            //Trigger state change for entering elements
            foreach (var item in viewItemForNextPage)
            {
                if (RePlayOnShowWhileSamePage && samePage)
                {
                    item.runtimeViewElement.OnShow();
                    continue;
                }

                //Apply models
                pageModelsCache = models;
                item.runtimeViewElement.ApplyModelInject();

                //Apply overrides
                item.runtimeViewElement.ApplyOverrides(item.overrideDatas);
                item.runtimeViewElement.ApplyEvents(item.eventDatas);

                var transformData = item.GetCurrentViewElementTransform(breakPointsStatus);

                if (!string.IsNullOrEmpty(transformData.parentPath))
                {
                    item.runtimeParent = transformCache.Find(transformData.parentPath);
                }
                else
                {
                    item.runtimeParent = vp.runtimePageRoot;
                }


                item.runtimeViewElement.ChangePage(true, item.runtimeParent, transformData, item.sortingOrder,
                    item.TweenTime, item.delayIn, reshowIfSamePage: RePlayOnShowWhileSamePage);
            }

            foreach (var item in viewItemForNextPage.OrderBy(m => m.sortingOrder))
            {
                item.runtimeViewElement.rectTransform.SetAsLastSibling();
            }

            SetNavigationTarget(vp);
            yield return runtimePool.RecoveryQueuedViewElement();
            //Fire the event
            OnChanged?.Invoke();
            InvokeOnOverlayPageShow(this, new ViewPageEventArgs(vp, null));

            //When all transitions are finished
            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(onShowTime + onShowDelay);
            else
                yield return Yielders.GetWaitForSeconds(onShowTime + onShowDelay);

            overlayPageStatus.IsTransition = false;

            OnComplete?.Invoke();
        }

        public override IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageStatus overlayPageState,
            float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false, bool ignoreClickProtection = false,
            bool ignoreTimeScale = false, bool waitForShowFinish = false)
        {
            if (waitForShowFinish &&
                overlayPageState.transition == ViewSystemUtilitys.OverlayPageStatus.Transition.Show)
            {
                ViewSystemLog.Log("Leave Overlay Page wait for pervious page");
                yield return new WaitUntil(() => !overlayPageState.IsTransition);
            }

            IEnumerable<ViewElement> currentVe = new List<ViewElement>();
            IEnumerable<ViewElement> currentVs = new List<ViewElement>();
            if (currentViewPage != null)
            {
                currentVe = currentViewPage.viewPageItems.Select(m => m.runtimeViewElement);
            }

            if (currentViewState != null)
            {
                currentVs = currentViewState.viewPageItems.Select(m => m.runtimeViewElement);
            }

            var finishTime =
                ViewSystemUtilitys.CalculateOnLeaveDuration(
                    overlayPageState.viewPage.viewPageItems.Select(m => m.runtimeViewElement));

            overlayPageState.transition = ViewSystemUtilitys.OverlayPageStatus.Transition.Leave;

            List<ViewPageItem> viewPageItems = new List<ViewPageItem>();

            viewPageItems.AddRange(overlayPageState.viewPage.viewPageItems);
            if (overlayPageState.viewState != null)
                viewPageItems.AddRange(overlayPageState.viewState.viewPageItems);

            foreach (var item in viewPageItems)
            {
                if (item.runtimeViewElement == null)
                {
                    var veName = item.viewElement != null ? item.viewElement.name : item.viewElementAddress ?? "(unknown)";
                    ViewSystemLog.LogWarning($"ViewElement : {veName} is null in runtime.");
                    continue;
                }

                // Unique ViewElements need special handling for borrowing
                if (item.runtimeViewElement.IsUnique == true && IsPageTransition == false)
                {
                    // Handle unique ViewElement between multiply overlay page
                    if (overlayPageStatusDict.Count > 1)
                    {
                        var overlayPageStatus = overlayPageStatusDict
                            .Where(o => o.Value.viewPage.canvasSortOrder < overlayPageState.viewPage.canvasSortOrder)
                            .Select(o => o.Value)
                            .OrderByDescending(o => o.viewPage.canvasSortOrder)
                            .FirstOrDefault(c => c != overlayPageState);
                        var vpi = overlayPageStatus?.viewPage.viewPageItems.FirstOrDefault(m =>
                            ReferenceEquals(m.runtimeViewElement, item.runtimeViewElement));

                        if (vpi != null)
                        {
                            try
                            {
                                var transformData = vpi.GetCurrentViewElementTransform(breakPointsStatus);
                                item.runtimeViewElement.ChangePage(true, vpi.runtimeParent, transformData,
                                    item.sortingOrder, tweenTimeIfNeed, 0);
                                ViewSystemLog.LogWarning("ViewElement : " + item.runtimeViewElement.name +
                                                         " Try to back to origin Transform parent : " +
                                                         vpi.runtimeParent.name);
                            }
                            catch
                            {
                            }

                            continue;
                        }
                    }

                    if (currentVe.Contains(item.runtimeViewElement))
                    {
                        //ViewElement scheduled for auto-leave is still in use by the current page, skip it
                        try
                        {
                            var vpi = currentViewPage.viewPageItems.FirstOrDefault(m =>
                                ReferenceEquals(m.runtimeViewElement, item.runtimeViewElement));

                            var transformData = vpi.GetCurrentViewElementTransform(breakPointsStatus);
                            if (!string.IsNullOrEmpty(transformData.parentPath))
                            {
                                vpi.runtimeParent = transformCache.Find(transformData.parentPath);
                            }
                            else
                            {
                                vpi.runtimeParent = currentViewPage.runtimePageRoot;
                            }

                            item.runtimeViewElement.ChangePage(true, vpi.runtimeParent, transformData,
                                item.sortingOrder, tweenTimeIfNeed, 0);
                            ViewSystemLog.LogWarning("ViewElement : " + item.viewElement.name +
                                                     "Try to back to origin Transfrom parent : " +
                                                     vpi.runtimeParent.name);
                        }
                        catch
                        {
                        }

                        continue;
                    }

                    if (currentVs.Contains(item.runtimeViewElement))
                    {
                        //ViewElement scheduled for auto-leave is still in use by the current page, skip it
                        try
                        {
                            var vpi = currentViewState.viewPageItems.FirstOrDefault(m =>
                                ReferenceEquals(m.runtimeViewElement, item.runtimeViewElement));

                            var transformData = vpi.GetCurrentViewElementTransform(breakPointsStatus);
                            if (!string.IsNullOrEmpty(transformData.parentPath))
                            {
                                vpi.runtimeParent = transformCache.Find(transformData.parentPath);
                            }
                            else
                            {
                                vpi.runtimeParent = currentViewPage.runtimePageRoot;
                            }

                            item.runtimeViewElement.ChangePage(true, vpi.runtimeParent, transformData,
                                item.sortingOrder, tweenTimeIfNeed, 0);
                            ViewSystemLog.LogWarning("ViewElement : " + item.runtimeViewElement.name +
                                                     "Try to back to origin Transfrom parent : " +
                                                     vpi.runtimeParent.name);
                        }
                        catch
                        {
                        }

                        continue;
                    }
                }

                // lastOverlayPageItemDelayOutTimes.TryGetValue(item.runtimeViewElement.name, out float delayOut);
                item.runtimeViewElement.ChangePage(false, null, null, item.sortingOrder, 0, 0, ignoreTransition);
            }


            yield return runtimePool.RecoveryQueuedViewElement();

            //Get Back the Navigation to CurrentPage
            SetNavigationTarget(currentViewPage);
            InvokeOnOverlayPageLeave(this, new ViewPageEventArgs(overlayPageState.viewPage, null));

            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(finishTime);
            else
                yield return Yielders.GetWaitForSeconds(finishTime);

            overlayPageState.IsTransition = false;

            string OverlayPageStateKey = GetOverlayStateKey(overlayPageState.viewPage);
            overlayPageStatusDict.Remove(OverlayPageStateKey);

            OnComplete?.Invoke();
        }

        /// <summary>
        /// Force refresh current FullPage, will call the method on each ViewElementBehaviour.RefreshView(); 
        /// </summary>
        public void RefreshAll()
        {
            RefreshFullPage();
            RefreshOverlayPage();
        }

        public void RefreshFullPage()
        {
            foreach (var item in currentLiveElements)
            {
                item.RefreshView();
            }
        }

        public void RefreshOverlayPage()
        {
            for (int i = 0; i < overlayPageStatusDict.Count; i++)
            {
                var item = overlayPageStatusDict.ElementAt(i);
                foreach (var element in item.Value.currentViewElements)
                {
                    element.RefreshView();
                }
            }
        }

        public bool IsFullPageLive(string viewPageName)
        {
            return currentViewPage != null && currentViewPage.name == viewPageName;
        }

        public bool IsViewStateLive(string viewStateName)
        {
            return currentViewPage != null && currentViewState.name == viewStateName;
        }

        public bool IsOverPageStateLive(string viewStateName, out string viewPageName, bool includeLeavingPage = false)
        {
            viewPageName = "";
            if (overlayPageStatusDict.TryGetValue(viewStateName,
                    out ViewSystemUtilitys.OverlayPageStatus overlayPageStatus))
            {
                viewPageName = overlayPageStatus.viewPage.name;
                if (overlayPageStatus.transition == ViewSystemUtilitys.OverlayPageStatus.Transition.Leave)
                {
                    return includeLeavingPage;
                }

                return true;
            }

            return false;
        }

        public bool IsOverPageLive(string viewPageName, bool includeLeavingPage = false)
        {
            if (string.IsNullOrEmpty(viewPageName))
            {
                return false;
            }

            if (viewPages == null)
            {
                return false;
            }

            if (!IsReady)
            {
                ViewSystemLog.LogWarning(
                    "ViewController is not ready ignore the call and will always return false until ready.");
                return false;
            }

            //Not found
            if (viewPages.TryGetValue(viewPageName, out ViewPage vp))
            {
                return IsOverPageLive(vp);
            }

            ViewSystemLog.LogError("No view page match " + viewPageName + " Found");
            return false;
        }

        public bool IsOverPageLive(ViewPage viewPage, bool includeLeavingPage = false)
        {
            string OverlayPageStateKey = GetOverlayStateKey(viewPage);

            if (overlayPageStatusDict.TryGetValue(OverlayPageStateKey,
                    out ViewSystemUtilitys.OverlayPageStatus overlayPageStatus))
            {
                if (overlayPageStatus.viewPage.name != viewPage.name)
                {
                    return false;
                }

                if (overlayPageStatus.transition == ViewSystemUtilitys.OverlayPageStatus.Transition.Leave)
                {
                    return includeLeavingPage;
                }

                return true;
            }

            return false;
        }

        public override void TryLeaveAllOverlayPage()
        {
            Debug.Log("TryLeaveAllOverlayPage");
            //Clear auto-leave list
            // base.TryLeaveAllOverlayPage();
            for (int i = 0; i < overlayPageStatusDict.Count; i++)
            {
                var item = overlayPageStatusDict.ElementAt(i);
                StartCoroutine(LeaveOverlayViewPageBase(item.Value, 0.4f, null, true));
            }
        }

        /// <summary>
        /// Try leave all overay except the specified viewpage in array
        /// </summary>
        /// <param name="ignoreOverlayPage"></param>
        public void TryLeaveAllOverlayPage(string[] ignoreOverlayPage)
        {
            for (int i = 0; i < overlayPageStatusDict.Count; i++)
            {
                var item = overlayPageStatusDict.ElementAt(i);
                if (ignoreOverlayPage.Contains(item.Value.viewPage.name))
                {
                    continue;
                }

                StartCoroutine(LeaveOverlayViewPageBase(item.Value, 0.4f, null, true));
            }
        }

        int lastFrameRate;

        void UpdateCurrentViewStateAndNotifyEvent(ViewPage vp)
        {
            lastViewPage = currentViewPage;
            currentViewPage = vp;

            SetNavigationTarget(vp);

            InvokeOnViewPageChange(this, new ViewPageEventArgs(currentViewPage, lastViewPage));
#if UNITY_EDITOR
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif

            if (!string.IsNullOrEmpty(vp.viewState) && viewStatesNames.Contains(vp.viewState) &&
                currentViewState?.name != vp.viewState)
            {
                lastViewState = currentViewState;
                // currentViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);
                viewStates.TryGetValue(vp.viewState, out ViewState _currentViewState);
                currentViewState = _currentViewState;
#if UNITY_EDITOR
                if (currentViewState.targetFrameRate != -1 &&
                    Application.targetFrameRate > currentViewState.targetFrameRate)
                {
                    lastFrameRate = Application.targetFrameRate;
                    Application.targetFrameRate = Mathf.Clamp(currentViewState.targetFrameRate, 15, 60);
                }
                else if (currentViewState.targetFrameRate == -1)
                {
                    Application.targetFrameRate = lastFrameRate;
                }

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif

                InvokeOnViewStateChange(this, new ViewStateEventArgs(currentViewState, lastViewState));
            }
        }
        
        public static Transform GetChildCanvasTransform(int index = 0)
        {
            if (index < 0 || index >= _childCanvasTransforms.Count)
            {
                ViewSystemLog.LogError($"Canvas index {index} is out of range. Returning root canvas transform.");
                return _childCanvasTransforms[0];
            }
            return _childCanvasTransforms[index];
        }
        
        public static Transform GetChildCanvasTransform(string name)
        {
            var transform = _childCanvasTransforms.FirstOrDefault(t => t.name == name);
            if (transform == null)
            {
                ViewSystemLog.LogError($"Canvas with name {name} not found. Returning root canvas transform.");
                return _childCanvasTransforms[0];
            }
            return transform;
        }

        #region Navigation

        void SetNavigationTarget(ViewPage vp)
        {
            if (vp != null && vp.IsNavigation && vp.firstSelected != null)
            {
                UnityEngine.EventSystems.EventSystem
                    .current.SetSelectedGameObject(vp.firstSelected.gameObject);
            }
        }

        /// <summary>
        /// Forcus the Navigation on target page,
        /// Note : only thi live view page will take effect and this function will not check the ViewPage live or not.
        /// </summary>
        /// <param name="vp"></param>
        public void SetUpNavigationOnViewPage(ViewPage vp)
        {
            DisableCurrentPageNavigation();
            DisableAllOverlayPageNavigation();

            var vpis = vp.viewPageItems;
            foreach (var vpi in vpis)
            {
                vpi.runtimeViewElement.ApplyNavigation(vpi.navigationDatas);
            }

            if (!string.IsNullOrEmpty(vp.viewState))
            {
                if (viewStates.TryGetValue(vp.viewState, out ViewState vs))
                {
                    List<ViewElementNavigationData> result;
                    var vpis_s = vs.viewPageItems;
                    foreach (var vpi in vpis_s)
                    {
                        if (vp.stateNavDict.TryGetValue(vpi.Id, out result))
                        {
                            vpi.runtimeViewElement.ApplyNavigation(result);
                        }
                    }
                }
            }
        }

        public void DisableCurrentPageNavigation()
        {
            if (currentViewPage != null)
            {
                var vpis = currentViewPage.viewPageItems;
                foreach (var vpi in vpis)
                {
                    vpi.runtimeViewElement.runtimeOverride.DisableNavigation();
                }
            }

            if (currentViewState != null)
            {
                var vpis = currentViewState.viewPageItems;
                foreach (var vpi in vpis)
                {
                    vpi.runtimeViewElement.runtimeOverride.DisableNavigation();
                }
            }
        }

        public void DisableAllOverlayPageNavigation()
        {
            foreach (var item in overlayPageStatusDict)
            {
                var vpis = item.Value.viewPage.viewPageItems;
                foreach (var vpi in vpis)
                {
                    vpi.runtimeViewElement.runtimeOverride.DisableNavigation();
                }

                if (item.Value.viewState != null)
                {
                    var vpis_s = item.Value.viewState.viewPageItems;
                    foreach (var vpi in vpis)
                    {
                        vpi.runtimeViewElement.runtimeOverride.DisableNavigation();
                    }
                }
            }
        }

        public override bool IsViewPageExsit(string viewPageName)
        {
            return viewPages.ContainsKey(viewPageName);
        }

        Dictionary<string, bool> breakPointsStatus = new Dictionary<string, bool>();

        public void SetBreakPoint(string breakPoint)
        {
            breakPointsStatus[breakPoint] = true;
            // if (!currentCustomBreakPoints.Contains(breakPoint)) currentCustomBreakPoints.Add(breakPoint);
        }

        public void RemoveBreakPoint(string breakPoint)
        {
            breakPointsStatus[breakPoint] = false;
            // currentCustomBreakPoints.Remove(breakPoint);
        }

        public void ClearBreakPoint()
        {
            breakPointsStatus.Clear();
        }

        public List<string> GetActiveBreakPoints()
        {
            var breakPoints = breakPointsStatus.Where(m => m.Value == true).Select(m => m.Key).ToList();
            return breakPoints;
        }

        public SafePadding.PerEdgeValues GetSafePaddingSetting(ViewPage vp)
        {
            if (vp.useGlobalSafePadding)
            {
                return viewSystemSaveData.globalSetting.edgeValues;
            }

            return vp.edgeValues;
        }

        #endregion

        #region Get ViewElement

        //Get ViewElement in viewPage
        public ViewElement GetViewPageElementByName(ViewPage viewPage, string viewPageItemName)
        {
            return viewPage.viewPageItems.SingleOrDefault((_) => _.displayName == viewPageItemName).runtimeViewElement;
        }

        public T GetViewPageElementComponentByName<T>(ViewPage viewPage, string viewPageItemName) where T : Component
        {
            return GetViewPageElementByName(viewPage, viewPageItemName).GetComponent<T>();
        }

        public ViewElement GetViewPageElementByName(string viewPageName, string viewPageItemName)
        {
            if (viewPages.TryGetValue(viewPageName, out ViewPage vp))
            {
                return GetViewPageElementByName(vp, viewPageItemName);
            }

            return null;
        }

        public T GetViewPageElementComponentByName<T>(string viewPageName, string viewPageItemName) where T : Component
        {
            return GetViewPageElementByName(viewPageName, viewPageItemName).GetComponent<T>();
        }

        public ViewElement GetCurrentViewPageElementByName(string viewPageItemName)
        {
            return GetViewPageElementByName(currentViewPage, viewPageItemName);
        }

        public T GetCurrentViewPageElementComponentByName<T>(string viewPageItemName) where T : Component
        {
            return GetCurrentViewPageElementByName(viewPageItemName).GetComponent<T>();
        }

        //Get viewElement in statePage

        public ViewElement GetViewStateElementByName(ViewState viewState, string viewStateItemName)
        {
            return viewState.viewPageItems.SingleOrDefault((_) => _.displayName == viewStateItemName)
                .runtimeViewElement;
        }

        public T GetViewStateElementComponentByName<T>(ViewState viewState, string viewStateItemName)
            where T : Component
        {
            return GetViewStateElementByName(viewState, viewStateItemName).GetComponent<T>();
        }

        public ViewElement GetViewStateElementByName(string viewStateName, string viewStateItemName)
        {
            //return GetViewStateElementByName(viewStates.SingleOrDefault(m => m.name == viewStateName), viewStateItemName);
            if (viewStates.TryGetValue(viewStateName, out ViewState vs))
            {
                return GetViewStateElementByName(vs, viewStateItemName);
            }

            return null;
        }

        public T GetViewStateElementComponentByName<T>(string viewStateName, string viewStateItemName)
            where T : Component
        {
            return GetViewStateElementByName(viewStateName, viewStateItemName).GetComponent<T>();
        }

        public ViewElement GetCurrentViewStateElementByName(string viewStateItemName)
        {
            return GetViewStateElementByName(currentViewState, viewStateItemName);
        }

        public T GetCurrentViewStateElementComponentByName<T>(string viewStateItemName) where T : Component
        {
            return GetCurrentViewStateElementByName(viewStateItemName).GetComponent<T>();
        }

        #endregion
    }
}