using System;
using System.Reflection;
using UnityEngine;
using RoR2;
using RoR2.UI;

namespace Pyro
{
	public class StatsDisplay : MonoBehaviour
	{
		private Transform MainCanvas;

		public GameObject Root;

		public GenericNotification GenericNotification;

		public Func<String> Title;

		public Func<String> Body;

		private void Awake()
		{
			this.MainCanvas = RoR2Application.instance.mainCanvas.transform;
			this.Root = Instantiate(Resources.Load<GameObject>("Prefabs/NotificationPanel2"));
			this.GenericNotification = this.Root.GetComponent<GenericNotification>();
			this.GenericNotification.transform.SetParent(this.MainCanvas);
			this.GenericNotification.iconImage.enabled = false;
		}

		private void Update()
		{
			if (this.GenericNotification == null) Destroy(this);
			else
			{
				typeof(LanguageTextMeshController).GetField("resolvedString", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this.GenericNotification.titleText, this.Title());
				typeof(LanguageTextMeshController).GetMethod("UpdateLabel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this.GenericNotification.titleText, new object[0]);
				typeof(LanguageTextMeshController).GetField("resolvedString", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this.GenericNotification.descriptionText, this.Body());
				typeof(LanguageTextMeshController).GetMethod("UpdateLabel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this.GenericNotification.descriptionText, new object[0]);
			}
		}

		private void OnDestroy()
		{
			Destroy(GenericNotification);
			Destroy(Root);
		}
	}
}
