// Copyright (c) 2015 SharpYaml - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// -------------------------------------------------------------------------------
// SharpYaml is a fork of YamlDotNet https://github.com/aaubry/YamlDotNet
// published with the following license:
// -------------------------------------------------------------------------------
// 
// Copyright (c) 2008, 2009, 2010, 2011, 2012 Antoine Aubry
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using SiliconStudio.Core.Reflection;

namespace SiliconStudio.Core.Yaml.Serialization.Descriptors
{
    /// <summary>
    /// Default implementation of a <see cref="IYamlTypeDescriptor"/>.
    /// </summary>
    public class YamlObjectDescriptor : ObjectDescriptorBase, IYamlTypeDescriptor
    {
        public static readonly Func<object, bool> ShouldSerializeDefault = o => true;

        private static readonly object[] EmptyObjectArray = new object[0];
        private readonly bool emitDefaultValues;
        private bool isSorted;
        private HashSet<string> remapMembers;

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlObjectDescriptor" /> class.
        /// </summary>
        /// <param name="attributeRegistry">The attribute registry.</param>
        /// <param name="type">The type.</param>
        /// <param name="emitDefaultValues">if set to <c>true</c> [emit default values].</param>
        /// <param name="namingConvention">The naming convention.</param>
        /// <exception cref="System.ArgumentNullException">type</exception>
        /// <exception cref="YamlException">type</exception>
        public YamlObjectDescriptor(IAttributeRegistry attributeRegistry, Type type, bool emitDefaultValues, IMemberNamingConvention namingConvention)
            : base(attributeRegistry, type)
        {
            if (attributeRegistry == null)
                throw new ArgumentNullException(nameof(attributeRegistry));
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (namingConvention == null)
                throw new ArgumentNullException(nameof(namingConvention));

            NamingConvention = namingConvention;
            this.emitDefaultValues = emitDefaultValues;

            Attributes = AttributeRegistry.GetAttributes(type);

            Style = DataStyle.Any;
            foreach (var attribute in Attributes)
            {
                var styleAttribute = attribute as DataStyleAttribute;
                if (styleAttribute != null)
                {
                    Style = styleAttribute.Style;
                }
            }
        }

        /// <summary>
        /// Gets attributes attached to this type.
        /// </summary>
        public List<Attribute> Attributes { get; }

        /// <summary>
        /// Gets the naming convention.
        /// </summary>
        /// <value>The naming convention.</value>
        public IMemberNamingConvention NamingConvention { get; }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <exception cref="YamlException">Failed to get ObjectDescriptor for type [{0}]. The member [{1}] cannot be registered as a member with the same name is already registered [{2}].DoFormat(type.FullName, member, existingMember)</exception>
        public override void Initialize()
        {
            if (members != null)
            {
                return;
            }

            members = PrepareMembers().ToArray();

            // If no members found, we don't need to build a dictionary map
            if (members.Length <= 0)
                return;

            mapMembers = new Dictionary<string, IMemberDescriptorBase>((int) (members.Length*1.2));

            foreach (var member in members.Cast<IYamlMemberDescriptor>())
            {
                IMemberDescriptorBase existingMember;
                if (mapMembers.TryGetValue(member.Name, out existingMember))
                {
                    throw new YamlException($"Failed to get ObjectDescriptor for type [{Type.FullName}]. The member [{member}] cannot be registered as a member with the same name is already registered [{existingMember}]");
                }

                mapMembers.Add(member.Name, member);

                // If there is any alternative names, register them
                if (member.AlternativeNames != null)
                {
                    foreach (var alternateName in member.AlternativeNames)
                    {
                        if (mapMembers.TryGetValue(alternateName, out existingMember))
                        {
                            throw new YamlException($"Failed to get ObjectDescriptor for type [{Type.FullName}]. The member [{member}] cannot be registered as a member with the same name [{alternateName}] is already registered [{existingMember}]");
                        }
                        else
                        {
                            if (remapMembers == null)
                            {
                                remapMembers = new HashSet<string>();
                            }

                            mapMembers[alternateName] = member;
                            remapMembers.Add(alternateName);
                        }
                    }
                }
            }
        }

        public override DescriptorCategory Category => DescriptorCategory.Object;


        public DataStyle Style { get; }

        /// <summary>
        /// Sorts the members of this instance with the specified instance.
        /// </summary>
        /// <param name="keyComparer">The key comparer.</param>
        public void SortMembers(IComparer<object> keyComparer)
        {
            if (keyComparer != null && !isSorted)
            {
                // TODO: avoid converting through a list
                var list = members.ToList();
                list.Sort(keyComparer.Compare);
                members = list.ToArray();
                isSorted = true;
            }
        }

        public bool IsMemberRemapped(string name)
        {
            return remapMembers != null && remapMembers.Contains(name);
        }

        protected override List<IMemberDescriptorBase> PrepareMembers()
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
            if (Category == DescriptorCategory.Object)
                bindingFlags |= BindingFlags.NonPublic;

            // Add all public properties with a readable get method
            var memberList = (from propertyInfo in Type.GetProperties(bindingFlags)
                where
                    propertyInfo.CanRead && propertyInfo.GetIndexParameters().Length == 0
                select new YamlPropertyDescriptor(propertyInfo, NamingConvention.Comparer)
                into member
                where PrepareMember(member)
                select member).Cast<IMemberDescriptorBase>().ToList();

            // Add all public fields
            memberList.AddRange((from fieldInfo in Type.GetFields(bindingFlags)
                select new YamlFieldDescriptor(fieldInfo, NamingConvention.Comparer)
                into member
                where PrepareMember(member)
                select member));

            // Allow to add dynamic members per type
            (AttributeRegistry as YamlAttributeRegistry)?.PrepareMembersCallback?.Invoke(this, memberList);

            return memberList;
        }

        protected virtual bool PrepareMember(YamlMemberDescriptorBase member)
        {
            var memberType = member.Type;

            // Remove all SyncRoot from members
            if (member is YamlPropertyDescriptor && member.OriginalName == "SyncRoot" &&
                (member.DeclaringType.Namespace ?? string.Empty).StartsWith(SystemCollectionsNamespace))
            {
                return false;
            }

            // Process all attributes just once instead of getting them one by one
            var attributes = AttributeRegistry.GetAttributes(member.MemberInfo);
            DataStyleAttribute styleAttribute = null;
            DataMemberAttribute memberAttribute = null;
            DefaultValueAttribute defaultValueAttribute = null;
            foreach (var attribute in attributes)
            {
                // Member is not displayed if there is a YamlIgnore attribute on it
                if (attribute is DataMemberIgnoreAttribute)
                {
                    return false;
                }

                var dataMemberAttribute = attribute as DataMemberAttribute;
                if (dataMemberAttribute != null)
                {
                    memberAttribute = dataMemberAttribute;
                    continue;
                }

                var valueAttribute = attribute as DefaultValueAttribute;
                if (valueAttribute != null)
                {
                    defaultValueAttribute = valueAttribute;
                    continue;
                }

                var dataStyleAttribute = attribute as DataStyleAttribute;
                if (dataStyleAttribute != null)
                {
                    styleAttribute = dataStyleAttribute;
                    continue;
                }

                var yamlRemap = attribute as DataAliasAttribute;
                if (yamlRemap != null)
                {
                    if (member.AlternativeNames == null)
                    {
                        member.AlternativeNames = new List<string>();
                    }
                    if (!string.IsNullOrWhiteSpace(yamlRemap.Name))
                    {
                        member.AlternativeNames.Add(yamlRemap.Name);
                    }
                }
            }

            // If the member has a set, this is a conventional assign method
            if (member.HasSet)
            {
                member.Mode = DataMemberMode.Content;
            }
            else
            {
                // Else we cannot only assign its content if it is a class
                member.Mode = (memberType != typeof(string) && memberType.IsClass) || memberType.IsInterface || Type.IsAnonymous() ? DataMemberMode.Content : DataMemberMode.Never;
            }

            // If it's a private member, check it has a YamlMemberAttribute on it
            if (!member.IsPublic)
            {
                if (memberAttribute == null)
                    return false;
            }

            // Gets the style
            member.Style = styleAttribute?.Style ?? DataStyle.Any;
            member.Mask = 1;

            // Handle member attribute
            if (memberAttribute != null)
            {
                member.Mask = memberAttribute.Mask;
                if (!member.HasSet)
                {
                    if (memberAttribute.Mode == DataMemberMode.Assign ||
                        (memberType.IsValueType && member.Mode == DataMemberMode.Content))
                        throw new ArgumentException($"{memberType.FullName} {member.OriginalName} is not writeable by {memberAttribute.Mode.ToString()}.");
                }

                if (memberAttribute.Mode != DataMemberMode.Default)
                {
                    member.Mode = memberAttribute.Mode;
                }
                member.Order = memberAttribute.Order;
            }

            if (member.Mode == DataMemberMode.Binary)
            {
                if (!memberType.IsArray)
                    throw new InvalidOperationException($"{memberType.FullName} {member.OriginalName} of {Type.FullName} is not an array. Can not be serialized as binary.");
                if (!memberType.GetElementType().IsPureValueType())
                    throw new InvalidOperationException($"{memberType.GetElementType()} is not a pure ValueType. {memberType.FullName} {member.OriginalName} of {Type.FullName} can not serialize as binary.");
            }

            // If this member cannot be serialized, remove it from the list
            if (member.Mode == DataMemberMode.Never)
            {
                return false;
            }

            // ShouldSerialize
            //	  YamlSerializeAttribute(Never) => false
            //	  ShouldSerializeSomeProperty => call it
            //	  DefaultValueAttribute(default) => compare to it
            //	  otherwise => true
            var shouldSerialize = Type.GetMethod("ShouldSerialize" + member.OriginalName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (shouldSerialize != null && shouldSerialize.ReturnType == typeof(bool) && member.ShouldSerialize == null)
                member.ShouldSerialize = obj => (bool) shouldSerialize.Invoke(obj, EmptyObjectArray);

            if (defaultValueAttribute != null && member.ShouldSerialize == null && !emitDefaultValues)
            {
                object defaultValue = defaultValueAttribute.Value;
                Type defaultType = defaultValue?.GetType();
                if (defaultType.IsNumeric() && defaultType != memberType)
                    defaultValue = memberType.CastToNumericType(defaultValue);
                member.ShouldSerialize = obj => !TypeExtensions.AreEqual(defaultValue, member.Get(obj));
            }

            if (member.ShouldSerialize == null)
                member.ShouldSerialize = ShouldSerializeDefault;

            member.Name = !string.IsNullOrEmpty(memberAttribute?.Name) ? memberAttribute.Name : NamingConvention.Convert(member.OriginalName);

            return true;
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }
}
