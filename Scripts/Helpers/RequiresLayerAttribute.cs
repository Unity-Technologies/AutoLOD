// From: https://github.com/Unity-Technologies/EditorVR
using System;

namespace UnityEditor.Experimental.AutoLOD
{
	sealed class RequiresLayerAttribute : Attribute
	{
		public string layer;

		public RequiresLayerAttribute(string layer)
		{
			this.layer = layer;
		}
	}
}
