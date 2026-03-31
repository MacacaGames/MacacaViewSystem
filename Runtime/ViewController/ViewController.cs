using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Linq;
using System;
using System.Reflection;

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
        [SerializeField] private ViewSystemSaveDataBase saveData;

        private Dictionary<string, AssetReferenceGameObject> _assetRefLookup;
        private Dictionary<string, AssetReferenceGameObject> _uniqueAssetRefLookup;
        private bool _useAddressableLoading = false;

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
        /// Dynamic load view system data (supports both ViewSystemSaveData and ViewSystemSaveData_Addressable)
        /// </summary>
        public void SetSaveDataManually(ViewSystemSaveDataBase data)
        {
            if (data == null)
            {
                ViewSystemLog.LogError("SetSaveDataManually called with null save data.");
                return;
            }

            this.saveData = data;
            Init();
        }

        public void Init()
        {
            // Return if is already init
            if (IsReady)
            {
                return;
            }

            if (saveData == null)
            {
                ViewSystemLog.LogError("No save data assigned. Set ViewSystemSaveData or ViewSystemSaveData_Addressable on ViewController.");
                return;
            }

            _useAddressableLoading = saveData.IsAddressableMode;
            var globalSetting = saveData.globalSetting;

            //Create ViewElementPool
            if (gameObject.name != globalSetting.ViewControllerObjectPath)
            {
                ViewSystemLog.LogWarning(
                    "The GameObject which attached ViewController is not match the setting in Base Setting.");
            }

            //Create UIRoot
            var uiRoot = Instantiate(globalSetting.UIRoot).transform;
            uiRoot.SetParent(transformCache);
            uiRoot.localPosition = globalSetting.UIRoot.transform.localPosition;
            uiRoot.gameObject.name = globalSetting.UIRoot.name;


            _childCanvasTransforms = uiRoot.GetComponentsInChildren<Canvas>().Select(canvas => canvas.transform).ToList();
            rootCanvasTransform = _childCanvasTransforms[0];

            if (!string.IsNullOrEmpty(globalSetting.customPageRootPath))
            {
                var target = rootCanvasTransform.Find(globalSetting.customPageRootPath);
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
            maxClampTime = globalSetting.MaxWaitingTime;
            minimumTimeInterval = globalSetting.minimumTimeInterval;
            builtInClickProtection = globalSetting.builtInClickProtection;
            try
            {
                breakPointsStatus = globalSetting.breakPoints.ToDictionary(m => m, m => false);
            }
            catch (Exception ex)
            {
                ViewSystemLog.LogError($"Error occur while proccess breakpoint {ex.Message}");
            }

            viewStates = saveData.GetViewStateSaveDatas().Select(m => m.viewState)
                .ToDictionary(m => m.name, m => m);
            viewPages = saveData.GetViewPageSaveDatas().Select(m => m.viewPage)
                .ToDictionary(m => m.name, m => m);

            if (_useAddressableLoading && saveData is ViewSystemSaveData_Addressable addressableSaveData)
            {
                // Build AssetReference lookup dictionaries
                _assetRefLookup = addressableSaveData.viewPageItemAssetRefs
                    .ToDictionary(x => x.viewPageItemId, x => x.assetReference);
                _uniqueAssetRefLookup = addressableSaveData.uniqueViewElementAssetRefs
                    .ToDictionary(x => x.type, x => x.assetReference);

                ViewSystemLog.Log($"ViewSystem Addressable mode enabled: {_assetRefLookup.Count} asset refs, {_uniqueAssetRefLookup.Count} unique refs.");
            }

            viewStatesNames = viewStates.Values.Select(m => m.name);

            if (autoPrewarm)
            {
                if (_useAddressableLoading)
                {
                    StartCoroutine(PrewarmSingletonViewElementAsync());
                }
                else
                {
                    PrewarmSingletonViewElement();
                    IsReady = true;
                }
            }
            else
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

        [System.Obsolete("GetInjectionInstance is obsolete, use GetSingletonViewElement instead")]
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
                    "Target type cannot been found, are you sure your ViewElement which attach target Component is unique?");
            }

            return null;
        }

        IViewElementSingleton WarmupUniqueViewElement(Type type)
        {
            if (_useAddressableLoading || saveData is not ViewSystemSaveData directSaveData)
            {
                ViewSystemLog.Log("In addressable mode, use GetSingletonViewElementAsync instead of sync warmup.");
                return null;
            }

            var item = directSaveData.uniqueViewElementTable.FirstOrDefault(m => m.type == type.ToString());
            IViewElementSingleton result = null;
            if (item == null || item.viewElementGameObject == null)
            {
                ViewSystemLog.Log("Cannot found matched type in the uniqueViewElementTable");
                return result;
            }

            var r = runtimePool.PrewarmUniqueViewElement(item.viewElementGameObject.GetComponent<ViewElement>());
            if (r != null)
            {
                foreach (var i in r.GetComponents<IViewElementSingleton>())
                {
                    if (i.GetType() == type)
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
                        continue;
                    }
                }
            }

            return result;
        }

        void PrewarmSingletonViewElement()
        {
            var viewElementsInStates = viewStates.Values.Select(m => m.viewPageItems).SelectMany(ma => ma)
                .Where(m => m.viewElement.IsUnique).Select(m => m.viewElement);
            var viewElementsInPages = viewPages.Values.Select(m => m.viewPageItems).SelectMany(ma => ma)
                .Where(m => m.viewElement.IsUnique).Select(m => m.viewElement);

            foreach (var item in viewElementsInStates)
            {
                if (item == null)
                {
                    ViewSystemLog.Log("I'm null!!!");
                    continue;
                }

                if (!item.IsUnique)
                {
                    continue;
                }

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
                        {
                            ViewSystemLog.LogWarning("Type " + t + " has been injected");
                            continue;
                        }
                    }
                }
            }

            foreach (var item in viewElementsInPages)
            {
                if (item == null)
                {
                    ViewSystemLog.Log("I'm null!!!");
                    continue;
                }

                if (!item.IsUnique)
                {
                    continue;
                }

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
                        {
                            ViewSystemLog.LogWarning("Type " + t + " has been injected");
                            continue;
                        }
                    }
                }
            }
        }

        IEnumerator PrewarmSingletonViewElementAsync()
        {
            var loadHandles = new List<(string type, AsyncOperationHandle<GameObject> handle)>();

            foreach (var entry in _uniqueAssetRefLookup)
            {
                if (!entry.Value.RuntimeKeyIsValid())
                {
                    ViewSystemLog.LogWarning($"Invalid AssetReference for unique element type: {entry.Key}");
                    continue;
                }

                var handle = entry.Value.LoadAssetAsync<GameObject>();
                loadHandles.Add((entry.Key, handle));
            }

            // Wait for all loads
            foreach (var (type, handle) in loadHandles)
            {
                yield return handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var ve = handle.Result.GetComponent<ViewElement>();
                    if (ve != null)
                    {
                        var r = runtimePool.PrewarmUniqueViewElement(ve);
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
                }
                else
                {
                    ViewSystemLog.LogError($"Failed to load unique ViewElement for type: {type}");
                }
            }

            ViewSystemLog.Log($"Async prewarm complete. SingletonViewElementDictionary has {SingletonViewElementDictionary.Count} entries.");
            IsReady = true;
        }

        /// <summary>
        /// Async version of GetSingletonViewElement for addressable mode.
        /// Use this when ViewElements may not be loaded yet.
        /// </summary>
        public IEnumerator GetSingletonViewElementAsync<T>(Action<T> callback) where T : Component, IViewElementSingleton
        {
            // Try sync path first
            if (SingletonViewElementDictionary.TryGetValue(typeof(T), out Component result))
            {
                callback?.Invoke((T)result);
                yield break;
            }

            // Try async load from Addressable lookup
            if (_useAddressableLoading && _uniqueAssetRefLookup.TryGetValue(typeof(T).ToString(), out var assetRef))
            {
                if (assetRef.RuntimeKeyIsValid())
                {
                    var handle = assetRef.LoadAssetAsync<GameObject>();
                    yield return handle;

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        var ve = handle.Result.GetComponent<ViewElement>();
                        if (ve != null)
                        {
                            var r = runtimePool.PrewarmUniqueViewElement(ve);
                            if (r != null)
                            {
                                foreach (var i in r.GetComponents<IViewElementSingleton>())
                                {
                                    var c = (Component)i;
                                    var t = c.GetType();
                                    if (!SingletonViewElementDictionary.ContainsKey(t))
                                        SingletonViewElementDictionary.Add(t, c);
                                }

                                if (SingletonViewElementDictionary.TryGetValue(typeof(T), out Component loaded))
                                {
                                    callback?.Invoke((T)loaded);
                                    yield break;
                                }
                            }
                        }
                    }
                }
            }

            ViewSystemLog.LogError($"Cannot find singleton ViewElement of type {typeof(T)}");
            callback?.Invoke(null);
        }

        /// <summary>
        /// Awaitable version of GetSingletonViewElementAsync.
        /// Returns the singleton ViewElement, loading it via Addressables if needed.
        /// </summary>
        public async System.Threading.Tasks.Task<T> GetSingletonViewElementAsync<T>() where T : Component, IViewElementSingleton
        {
            // Try sync path first
            if (SingletonViewElementDictionary.TryGetValue(typeof(T), out Component result))
            {
                return (T)result;
            }

            // Try sync warmup (works for V1 direct reference mode)
            if (!_useAddressableLoading)
            {
                IViewElementSingleton s = WarmupUniqueViewElement(typeof(T));
                if (s != null)
                {
                    return (T)s;
                }
            }

            // Try async load from Addressable lookup
            if (_useAddressableLoading && _uniqueAssetRefLookup.TryGetValue(typeof(T).ToString(), out var assetRef))
            {
                if (assetRef.RuntimeKeyIsValid())
                {
                    var handle = assetRef.LoadAssetAsync<GameObject>();
                    await handle.Task;

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        var ve = handle.Result.GetComponent<ViewElement>();
                        if (ve != null)
                        {
                            var r = runtimePool.PrewarmUniqueViewElement(ve);
                            if (r != null)
                            {
                                foreach (var i in r.GetComponents<IViewElementSingleton>())
                                {
                                    var c = (Component)i;
                                    var t = c.GetType();
                                    if (!SingletonViewElementDictionary.ContainsKey(t))
                                        SingletonViewElementDictionary.Add(t, c);
                                }

                                if (SingletonViewElementDictionary.TryGetValue(typeof(T), out Component loaded))
                                {
                                    return (T)loaded;
                                }
                            }
                        }
                    }
                }
            }

            ViewSystemLog.LogError($"Cannot find singleton ViewElement of type {typeof(T)}");
            return null;
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

        IEnumerator PrepareRuntimeReferenceAsync(IEnumerable<ViewPageItem> viewPageItems, Action<IEnumerable<ViewPageItem>> onComplete)
        {
            var itemList = viewPageItems.ToList();
            var loadHandles = new List<(ViewPageItem item, AsyncOperationHandle<GameObject> handle)>();

            foreach (var item in itemList)
            {
                if (_assetRefLookup.TryGetValue(item.Id, out var assetRef) && assetRef.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<GameObject> handle;
                    if (assetRef.OperationHandle.IsValid())
                    {
                        handle = assetRef.OperationHandle.Convert<GameObject>();
                    }
                    else
                    {
                        handle = assetRef.LoadAssetAsync<GameObject>();
                    }
                    loadHandles.Add((item, handle));
                }
                else if (item.viewElement != null)
                {
                    // Fallback to direct reference if available
                    item.runtimeViewElement = runtimePool.RequestViewElement(item.viewElement);
                }
                else
                {
                    ViewSystemLog.LogError($"No AssetReference found for ViewPageItem: {item.Id} ({item.displayName})");
                }
            }

            // Wait for all async loads
            foreach (var (item, handle) in loadHandles)
            {
                if (!handle.IsDone)
                    yield return handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var ve = handle.Result.GetComponent<ViewElement>();
                    if (ve != null)
                    {
                        item.runtimeViewElement = runtimePool.RequestViewElement(ve);
                    }
                    else
                    {
                        ViewSystemLog.LogError($"Loaded asset for ViewPageItem '{item.displayName}' does not have ViewElement component.");
                    }
                }
                else
                {
                    ViewSystemLog.LogError($"Failed to load ViewElement for ViewPageItem: {item.Id} ({item.displayName})");
                }
            }

            onComplete?.Invoke(itemList);
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
            // Get the ViewPage object
            ViewPage nextViewPageForCurrentChangePage = null;

            // Not found
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
                saveData.globalSetting.UIPageTransformLayerName);
            nextViewPageForCurrentChangePage.runtimePageRoot = pageWrapper.rectTransform;

            pageWrapper.safePadding.SetPaddingValue(GetSafePaddingSetting(nextViewPageForCurrentChangePage));

            // pageWrapper.safePadding.SetPaddingValue(nextViewPageForCurrentChangePage.edgeValues);

            // All checks passed, start page transition
            //IsPageTransition = true;

            nextViewState = null;
            viewStates.TryGetValue(nextViewPageForCurrentChangePage.viewState, out ViewState _nextViewState);
            nextViewState = _nextViewState;

            IEnumerable<ViewPageItem> viewItemNextPage = null;
            IEnumerable<ViewPageItem> viewItemNextState = GetAllViewPageItemInViewState(nextViewState);
            List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem>();

            if (_useAddressableLoading)
            {
                yield return PrepareRuntimeReferenceAsync(
                    GetAllViewPageItemInViewPage(nextViewPageForCurrentChangePage),
                    result => { viewItemNextPage = result; });

                if (_nextViewState != currentViewState)
                {
                    yield return PrepareRuntimeReferenceAsync(viewItemNextState,
                        result => { viewItemNextState = result; });
                }
            }
            else
            {
                viewItemNextPage = PrepareRuntimeReference(GetAllViewPageItemInViewPage(nextViewPageForCurrentChangePage));
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
                // If not present in next page, add to removal list
                if (allViewElementForNextPageInViewPage.Contains(item) == false &&
                    allViewElementForNextPageInViewState.Contains(item) == false)
                {
                    viewElementDoesExitsInNextPage.Add(item);
                }
            }

            currentLiveElementsInViewPage.Clear();
            currentLiveElementsInViewPage = allViewElementForNextPageInViewPage;

            if (_nextViewState != currentViewState)
            {
                foreach (var item in currentLiveElementsInViewState)
                {
                    // If not present in next page, add to removal list
                    if (allViewElementForNextPageInViewState.Contains(item) == false &&
                        allViewElementForNextPageInViewPage.Contains(item) == false)
                    {
                        viewElementDoesExitsInNextPage.Add(item);
                    }
                }

                currentLiveElementsInViewState.Clear();
                currentLiveElementsInViewState = allViewElementForNextPageInViewState;
            }

            // Notify leaving elements to change state
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

            // Wait for previous page OnLeave to finish. Note: with many Animators, this is an estimated duration clamped to max time
            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(TimeForPerviousPageOnLeave);
            else
                yield return Yielders.GetWaitForSeconds(TimeForPerviousPageOnLeave);

            viewItemForNextPage.AddRange(viewItemNextPage);
            if (viewItemNextState != null) viewItemForNextPage.AddRange(viewItemNextState);
            // Notify entering elements to change state (ViewPage)
            foreach (var item in viewItemForNextPage.OrderBy(m => m.sortingOrder))
            {
                if (item.runtimeViewElement == null)
                {
                    ViewSystemLog.LogError($"The runtimeViewElement is null for some reason, ignore this item.");
                    continue;
                }

                // Apply models
                pageModelsCache = models;
                item.runtimeViewElement.ApplyModelInject();

                // Apply overrides
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

            // Update state
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
                // Notify event
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
                    saveData.globalSetting.UIPageTransformLayerName);
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
            // Check if an Overlay page with the same State is already on screen
            if (overlayPageStatusDict.TryGetValue(OverlayPageStateKey,
                    out ViewSystemUtilitys.OverlayPageStatus overlayPageStatus))
            {
                if (_useAddressableLoading)
                {
                    yield return PrepareRuntimeReferenceAsync(GetAllViewPageItemInViewPage(vp), result => { viewItemNextPage = result; });
                }
                else
                {
                    viewItemNextPage = PrepareRuntimeReference(GetAllViewPageItemInViewPage(vp));
                }

                // Same OverlayState page is already on screen, remove different parts and show new ones
                if (!string.IsNullOrEmpty(vp.viewState))
                {
                    if (overlayPageStatus.viewPage.name != vp.name)
                    {
                        // Same State but different Page, find the differences
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
                    // ViewPage-only entry means the page is still on screen
                    // If RePlayOnShowWhileSamePage == false, update values so stop the old coroutine
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
                // Same OverlayState page is not on screen yet, create a new status

                overlayPageStatus = new ViewSystemUtilitys.OverlayPageStatus();
                overlayPageStatus.viewPage = vp;
                overlayPageStatus.viewState = viewState;
                overlayPageStatus.transition = ViewSystemUtilitys.OverlayPageStatus.Transition.Show;

                // Register early so that Leave calls during async loading can find this entry
                overlayPageStatusDict[OverlayPageStateKey] = overlayPageStatus;

                if (_useAddressableLoading)
                {
                    yield return PrepareRuntimeReferenceAsync(GetAllViewPageItemInViewPage(vp), result => { viewItemNextPage = result; });
                }
                else
                {
                    viewItemNextPage = PrepareRuntimeReference(GetAllViewPageItemInViewPage(vp));
                }

                // Pages without viewState don't need to process viewState runtimeViewElements
                if (!string.IsNullOrEmpty(vp.viewState))
                {
                    // nextViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);
                    if (viewStates.TryGetValue(vp.viewState, out ViewState _nextViewState))
                    {
                        nextViewState = _nextViewState;
                        viewItemNextState = GetAllViewPageItemInViewState(nextViewState);
                        if (_useAddressableLoading)
                        {
                            yield return PrepareRuntimeReferenceAsync(viewItemNextState, result => { viewItemNextState = result; });
                        }
                        else
                        {
                            viewItemNextState = PrepareRuntimeReference(viewItemNextState);
                        }
                    }
                }
            }

            OnStart?.Invoke();

            if (viewItemNextState != null) viewItemForNextPage.AddRange(viewItemNextState);
            viewItemForNextPage.AddRange(viewItemNextPage);


            float onShowTime =
                ViewSystemUtilitys.CalculateOnShowDuration(viewItemNextPage.Select(m => m.runtimeViewElement));
            float onShowDelay = ViewSystemUtilitys.CalculateDelayInTime(viewItemNextPage);

            // Notify leaving elements to change state
            foreach (var item in viewElementDoesExitsInNextPage)
            {
                // Debug.LogWarning($"{item.name} not exsit in next page");

                item.ChangePage(false, null, null, 0, 0, 0);
            }

            // Notify entering elements to change state
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

                // Apply overrides
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

            // When all animations are finished
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
                    ViewSystemLog.LogWarning($"ViewElement : {item.viewElement.name} is null in runtime.");
                    continue;
                }

                // Handle unique ViewElement borrowing separately
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
                                ViewSystemLog.LogWarning("ViewElement : " + item.viewElement.name +
                                                         "Try to back to origin Transfrom parent : " +
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
                        // The ViewElement is currently in use by the active page, do not modify it
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
                        // The ViewElement is currently in use by the active page, do not modify it
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

            // Not found
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
            // Clear all auto-leaving overlay pages
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
                return saveData.globalSetting.edgeValues;
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