﻿using System;
using System.Linq;
using UnityEngine;
using UnityWeld.Binding.Internal;

namespace UnityWeld.Binding
{
    /// <summary>
    /// Base class for binders to Unity MonoBehaviours.
    /// </summary>
    [HelpURL("https://github.com/Real-Serious-Games/Unity-Weld")]
    public abstract class AbstractMemberBinding : MonoBehaviour, IMemberBinding
    {
        /// <summary>
        /// Initialise this binding. Used when we first start the scene.
        /// Detaches any attached view models, finds available view models afresh and then connects the binding.
        /// </summary>
        public virtual void Init()
        {
            Disconnect();

            Connect();
        }

        /// <summary>
        /// Scan up the hierarchy and find a view model that corresponds to the specified name.
        /// </summary>
        private object FindViewModel(string viewModelName)
        {
            var trans = transform;
            while (trans != null)
            {
                var components = trans.GetComponents<MonoBehaviour>();
                var monoBehaviourViewModel = components
                    .FirstOrDefault(component => component.GetType().ToString() == viewModelName);
                if (monoBehaviourViewModel != null)
                {
                    return monoBehaviourViewModel;
                }

                var providedViewModel = components
                    .Select(component => component as IViewModelProvider)
                    .Where(component => component != null)
                    .FirstOrDefault(
                        viewModelBinding => viewModelBinding.GetViewModelTypeName() == viewModelName && 
#pragma warning disable 252,253 // Warning says unintended reference comparison, but we do want to compare references
                        (object)viewModelBinding != this
#pragma warning restore 252,253
                    );

                if (providedViewModel != null)
                {
                    return providedViewModel.GetViewModel();
                }

                trans = trans.parent;
            }

            throw new ApplicationException(string.Format("Tried to get view model {0} but it could not be found on "
                + "object {1}. Check that a ViewModelBinding for that view model exists further up in "
                + "the scene hierarchy. ", viewModelName, gameObject.name)
            );
        }

        /// <summary>
        /// Find the type of the adapter with the specified name and create it.
        /// </summary>
        protected static IAdapter CreateAdapter(string adapterTypeName)
        {
            if (string.IsNullOrEmpty(adapterTypeName))
            {
                return null;
            }

            var adapterType = TypeResolver.FindAdapterType(adapterTypeName);
            if (adapterType == null)
            {
                throw new ApplicationException("Could not find adapter type '" + adapterTypeName + "'.");
            }

            if (!typeof(IAdapter).IsAssignableFrom(adapterType))
            {
                throw new ApplicationException("Type '" + adapterTypeName + "' does not implement IAdapter and cannot be used as an adapter.");
            }

            return (IAdapter)Activator.CreateInstance(adapterType);
        }

        /// <summary>
        /// Make a property end point for a property on the view model.
        /// </summary>
        protected PropertyEndPoint MakeViewModelEndPoint(string viewModelPropertyName, string adapterTypeName, AdapterOptions adapterOptions)
        {
            string propertyName;
            object viewModel;
            ParseViewModelEndPointReference(viewModelPropertyName, out propertyName, out viewModel);

            var adapter = CreateAdapter(adapterTypeName);

            return new PropertyEndPoint(viewModel, propertyName, adapter, adapterOptions, "view-model", this);
        }

        /// <summary>
        /// Parse an end-point reference including a type name and member name separated by a period.
        /// </summary>
        protected static void ParseEndPointReference(string endPointReference, out string memberName, out string typeName)
        {
            var lastPeriodIndex = endPointReference.LastIndexOf('.');
            if (lastPeriodIndex == -1)
            {
                throw new ApplicationException("No period was found, expected end-point reference in the following format: <type-name>.<member-name>.");
            }

            typeName = endPointReference.Substring(0, lastPeriodIndex);
            memberName = endPointReference.Substring(lastPeriodIndex + 1);
            if (typeName.Length == 0 || memberName.Length == 0)
            {
                throw new ApplicationException("Bad format for end-point reference, expected the following format: <type-name>.<member-name>.");
            }
        }

        /// <summary>
        /// Parse an end-point reference and search up the hierarchy for the named view-model.
        /// </summary>
        protected void ParseViewModelEndPointReference(string endPointReference, out string memberName, out object viewModel)
        {
            string viewModelName;
            ParseEndPointReference(endPointReference, out memberName, out viewModelName);

            viewModel = FindViewModel(viewModelName);
            if (viewModel == null)
            {
                throw new ApplicationException("Failed to find view-model in hierarchy: " + viewModelName);
            }
        }

        /// <summary>
        /// Parse an end-point reference and get the component for the view.
        /// </summary>
        protected void ParseViewEndPointReference(string endPointReference, out string memberName, out Component view)
        {
            string boundComponentType;
            ParseEndPointReference(endPointReference, out memberName, out boundComponentType);

            view = GetComponent(boundComponentType);
            if (view == null)
            {
                throw new ApplicationException("Failed to find component on current game object: " + boundComponentType);
            }
        }

        /// <summary>
        /// Connect to all the attached view models
        /// </summary>
        public abstract void Connect();

        /// <summary>
        /// Disconnect from all attached view models.
        /// </summary>
        public abstract void Disconnect();

        protected void Awake()
        {
            Init();
        }

        /// <summary>
        /// Clean up when the game object is destroyed.
        /// </summary>
        public void OnDestroy()
        {
            Disconnect();
        }
    }
}
