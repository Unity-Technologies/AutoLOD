// From: https://github.com/Unity-Technologies/EditorVR
using System;

namespace Unity.AutoLOD
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	sealed class RequiresTagAttribute : Attribute
	{
		public string tag;

		public RequiresTagAttribute(string tag)
		{
			this.tag = tag;
		}
	}
}
