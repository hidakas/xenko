﻿using System;

namespace SiliconStudio.Core.Updater
{
    public class UpdatableProperty<T> : UpdatableProperty
    {
        public static Func<UpdatableMember> StaticCreateMemberElement;

        public override UpdatableMember CreateMemberElement()
        {
            return StaticCreateMemberElement();
        }

        public UpdatableProperty(IntPtr getter, IntPtr setter)
        {
            Getter = getter;
            Setter = setter;
        }

        public override Type MemberType
        {
            get { return typeof(T); }
        }

        public override IntPtr GetStructAndUnbox(IntPtr obj, object data)
        {
#if IL
            ldarg data
            unbox !T
            dup
            ldarg obj
            ldarg.0
            ldfld native int class SiliconStudio.Core.Updater.UpdatableProperty::Getter
            calli instance !T()
            stobj !T
            ret
#endif
            throw new NotImplementedException();
        }

        public override void GetBlittable(IntPtr obj, IntPtr data)
        {
#if IL
            ldarg data
            ldarg obj
            ldarg.0
            ldfld native int class SiliconStudio.Core.Updater.UpdatableProperty::Getter
            calli instance !T()
            stobj !T
            ret
#endif
            throw new NotImplementedException();
        }

        public override void SetStruct(IntPtr obj, object data)
        {
#if IL
            ldarg obj
            ldarg data
            unbox.any !T
            ldarg.0
            ldfld native int class SiliconStudio.Core.Updater.UpdatableProperty::Setter
            calli instance void(!T)
            ret
#endif
            throw new NotImplementedException();
        }

        public override void SetBlittable(IntPtr obj, IntPtr data)
        {
#if IL
            ldarg obj
            ldarg data
            ldobj !T
            ldarg.0
            ldfld native int class SiliconStudio.Core.Updater.UpdatableProperty::Setter
            calli instance void(!T)
            ret
#endif
            throw new NotImplementedException();
        }
    }
}