// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    /// <summary>
    /// This class represents the live NON public INTERNAL object in the system
    /// </summary>
    public class PrivateObject
    {
        // bind everything
        private const BindingFlags BindToEveryThing = BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

        private static BindingFlags constructorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.NonPublic;

        private object target;     // automatically initialized to null
        private Type originalType; // automatically initialized to null

        private Dictionary<string, LinkedList<MethodInfo>> methodCache; // automatically initialized to null

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateObject"/> class that contains
        /// the already existing object of the private class
        /// </summary>
        /// <param name="obj"> object that serves as starting point to reach the private members</param>
        /// <param name="memberToAccess">the derefrencing string using . that points to the object to be retrived as in m_X.m_Y.m_Z</param>
        public PrivateObject(object obj, string memberToAccess)
        {
            ValidateAccessString(memberToAccess);

            PrivateObject temp = obj as PrivateObject;
            if (temp == null)
            {
                temp = new PrivateObject(obj);
            }

            // Split The access string
            string[] arr = memberToAccess.Split(new char[] { '.' });

            for (int i = 0; i < arr.Length; i++)
            {
                object next = temp.InvokeHelper(arr[i], BindToEveryThing | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty, null, CultureInfo.InvariantCulture);
                temp = new PrivateObject(next);
            }

            this.target = temp.target;
            this.originalType = temp.originalType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
        /// specified type.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly</param>
        /// <param name="typeName">fully qualified name</param>
        /// <param name="args">Argmenets to pass to the constructor</param>
        public PrivateObject(string assemblyName, string typeName, params object[] args)
            : this(assemblyName, typeName, null, args)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
        /// specified type.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly</param>
        /// <param name="typeName">fully qualified name</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the constructor to get</param>
        /// <param name="args">Argmenets to pass to the constructor</param>
        public PrivateObject(string assemblyName, string typeName, Type[] parameterTypes, object[] args)
            : this(Type.GetType(string.Format(CultureInfo.InvariantCulture, "{0}, {1}", typeName, assemblyName), false), parameterTypes, args)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
        /// specified type.
        /// </summary>
        /// <param name="type">type of the object to create</param>
        /// <param name="args">Argmenets to pass to the constructor</param>
        public PrivateObject(Type type, params object[] args)
            : this(type, null, args)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps the
        /// specified type.
        /// </summary>
        /// <param name="type">type of the object to create</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the constructor to get</param>
        /// <param name="args">Argmenets to pass to the constructor</param>
        public PrivateObject(Type type, Type[] parameterTypes, object[] args)
        {
            object o;
            if (parameterTypes != null)
            {
                ConstructorInfo ci = type.GetConstructor(BindToEveryThing, null, parameterTypes, null);
                if (ci == null)
                {
                    throw new ArgumentException("The constructor with the specified signature could not be found. You might need to regenerate your private accessor, or the member may be private and defined on a base class. If the latter is true, you need to pass the type that defines the member into PrivateObject's constructor.");
                }

                try
                {
                    o = ci.Invoke(args);
                }
                catch (TargetInvocationException e)
                {
                    Debug.Assert(e.InnerException != null, "Inner exception should not be null.");
                    if (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }

                    throw;
                }
            }
            else
            {
                o = Activator.CreateInstance(type, constructorFlags, null, args, null);
            }

            this.ConstructFrom(o);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps
        /// the given object.
        /// </summary>
        /// <param name="obj">object to wrap</param>
        public PrivateObject(object obj)
        {
            this.ConstructFrom(obj);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateObject"/> class that wraps
        /// the given object.
        /// </summary>
        /// <param name="obj">object to wrap</param>
        /// <param name="type">PrivateType object</param>
        public PrivateObject(object obj, PrivateType type)
        {
            this.target = obj;
            this.originalType = type.ReferencedType;
        }

        /// <summary>
        /// Gets or sets the target
        /// </summary>
        public object Target
        {
            get
            {
                return this.target;
            }

            set
            {
                this.target = value;
                this.originalType = value.GetType();
            }
        }

        /// <summary>
        /// Gets the type of underlying object
        /// </summary>
        public Type RealType
        {
            get
            {
                return this.originalType;
            }
        }

        private Dictionary<string, LinkedList<MethodInfo>> GenericMethodCache
        {
            get
            {
                if (this.methodCache == null)
                {
                    this.BuildGenericMethodCacheForType(this.originalType);
                }

                Debug.Assert(this.methodCache != null, "Invalid method cache for type.");

                return this.methodCache;
            }
        }

        /// <summary>
        /// returns the hash code of the target object
        /// </summary>
        /// <returns>int representing hashcode of the target object</returns>
        public override int GetHashCode()
        {
            Debug.Assert(this.target != null, "target should not be null.");
            return this.target.GetHashCode();
        }

        /// <summary>
        /// Equals
        /// </summary>
        /// <param name="obj">Object with whom to compare</param>
        /// <returns>returns true if the objects are equal.</returns>
        public override bool Equals(object obj)
        {
            if (this != obj)
            {
                Debug.Assert(this.target != null, "target should not be null.");
                if (typeof(PrivateObject) == obj?.GetType())
                {
                    return this.target.Equals(((PrivateObject)obj).target);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, params object[] args)
        {
            return this.Invoke(name, null, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, Type[] parameterTypes, object[] args)
        {
            return this.Invoke(name, parameterTypes, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, Type[] parameterTypes, object[] args, Type[] typeArguments)
        {
            return this.Invoke(name, BindToEveryThing, parameterTypes, args, CultureInfo.InvariantCulture, typeArguments);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <param name="culture">Culture info</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, object[] args, CultureInfo culture)
        {
            return this.Invoke(name, null, args, culture);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <param name="culture">Culture info</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, Type[] parameterTypes, object[] args, CultureInfo culture)
        {
            return this.Invoke(name, BindToEveryThing, parameterTypes, args, culture);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, BindingFlags bindingFlags, params object[] args)
        {
            return this.Invoke(name, bindingFlags, null, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, BindingFlags bindingFlags, Type[] parameterTypes, object[] args)
        {
            return this.Invoke(name, bindingFlags, parameterTypes, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <param name="culture">Culture info</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, BindingFlags bindingFlags, object[] args, CultureInfo culture)
        {
            return this.Invoke(name, bindingFlags, null, args, culture);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <param name="culture">Culture info</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, BindingFlags bindingFlags, Type[] parameterTypes, object[] args, CultureInfo culture)
        {
            return this.Invoke(name, bindingFlags, parameterTypes, args, culture, null);
        }

        /// <summary>
        /// Invokes the specified method
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to get.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <param name="culture">Culture info</param>
        /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
        /// <returns>Result of method call</returns>
        public object Invoke(string name, BindingFlags bindingFlags, Type[] parameterTypes, object[] args, CultureInfo culture, Type[] typeArguments)
        {
            if (parameterTypes != null)
            {
                bindingFlags |= BindToEveryThing | BindingFlags.Instance;

                // Fix up the parameter types
                MethodInfo member = this.originalType.GetMethod(name, bindingFlags, null, parameterTypes, null);

                // If the method was not found and type arguments were provided for generic paramaters,
                // attempt to look up a generic method.
                if ((member == null) && (typeArguments != null))
                {
                    // This method may contain generic parameters...if so, the previous call to
                    // GetMethod() will fail because it doesn't fully support generic parameters.

                    // Look in the method cache to see if there is a generic method
                    // on the incoming type that contains the correct signature.
                    member = this.GetGenericMethodFromCache(name, parameterTypes, typeArguments, bindingFlags, null);
                }

                if (member == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.CurrentCulture, "The member specified ({0}) could not be found. You might need to regenerate your private accessor, or the member may be private and defined on a base class. If the latter is true, you need to pass the type that defines the member into PrivateObject's constructor.", name));
                }

                try
                {
                    if (member.IsGenericMethodDefinition)
                    {
                        MethodInfo constructed = member.MakeGenericMethod(typeArguments);
                        return constructed.Invoke(this.target, bindingFlags, null, args, culture);
                    }
                    else
                    {
                        return member.Invoke(this.target, bindingFlags, null, args, culture);
                    }
                }
                catch (TargetInvocationException e)
                {
                    Debug.Assert(e.InnerException != null, "Inner exception should not be null.");
                    if (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }

                    throw;
                }
            }
            else
            {
                return this.InvokeHelper(name, bindingFlags | BindingFlags.InvokeMethod, args, culture);
            }
        }

        /// <summary>
        /// Gets the array element using array of subsrcipts for each dimension
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="indices">the indices of array</param>
        /// <returns>An arrya of elements.</returns>
        public object GetArrayElement(string name, params int[] indices)
        {
            return this.GetArrayElement(name, BindToEveryThing, indices);
        }

        /// <summary>
        /// Sets the array element using array of subsrcipts for each dimension
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="value">Value to set</param>
        /// <param name="indices">the indices of array</param>
        public void SetArrayElement(string name, object value, params int[] indices)
        {
            this.SetArrayElement(name, BindToEveryThing, value, indices);
        }

        /// <summary>
        /// Gets the array element using array of subsrcipts for each dimension
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="indices">the indices of array</param>
        /// <returns>An arrya of elements.</returns>
        public object GetArrayElement(string name, BindingFlags bindingFlags, params int[] indices)
        {
            Array arr = (Array)this.InvokeHelper(name, BindingFlags.GetField | bindingFlags, null, CultureInfo.InvariantCulture);
            return arr.GetValue(indices);
        }

        /// <summary>
        /// Sets the array element using array of subsrcipts for each dimension
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="value">Value to set</param>
        /// <param name="indices">the indices of array</param>
        public void SetArrayElement(string name, BindingFlags bindingFlags, object value, params int[] indices)
        {
            Array arr = (Array)this.InvokeHelper(name, BindingFlags.GetField | bindingFlags, null, CultureInfo.InvariantCulture);
            arr.SetValue(value, indices);
        }

        /// <summary>
        /// Get the field
        /// </summary>
        /// <param name="name">Name of the field</param>
        /// <returns>The field.</returns>
        public object GetField(string name)
        {
            return this.GetField(name, BindToEveryThing);
        }

        /// <summary>
        /// Sets the field
        /// </summary>
        /// <param name="name">Name of the field</param>
        /// <param name="value">value to set</param>
        public void SetField(string name, object value)
        {
            this.SetField(name, BindToEveryThing, value);
        }

        /// <summary>
        /// Gets the field
        /// </summary>
        /// <param name="name">Name of the field</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <returns>The field.</returns>
        public object GetField(string name, BindingFlags bindingFlags)
        {
            return this.InvokeHelper(name, BindingFlags.GetField | bindingFlags, null, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sets the field
        /// </summary>
        /// <param name="name">Name of the field</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="value">value to set</param>
        public void SetField(string name, BindingFlags bindingFlags, object value)
        {
            this.InvokeHelper(name, BindingFlags.SetField | bindingFlags, new object[] { value }, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Get the field or property
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <returns>The field or property.</returns>
        public object GetFieldOrProperty(string name)
        {
            return this.GetFieldOrProperty(name, BindToEveryThing);
        }

        /// <summary>
        /// Sets the field or property
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <param name="value">value to set</param>
        public void SetFieldOrProperty(string name, object value)
        {
            this.SetFieldOrProperty(name, BindToEveryThing, value);
        }

        /// <summary>
        /// Gets the field or property
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <returns>The field or property.</returns>
        public object GetFieldOrProperty(string name, BindingFlags bindingFlags)
        {
            return this.InvokeHelper(name, BindingFlags.GetField | BindingFlags.GetProperty | bindingFlags, null, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sets the field or property
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="value">value to set</param>
        public void SetFieldOrProperty(string name, BindingFlags bindingFlags, object value)
        {
            this.InvokeHelper(name, BindingFlags.SetField | BindingFlags.SetProperty | bindingFlags, new object[] { value }, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>The property.</returns>
        public object GetProperty(string name, params object[] args)
        {
            return this.GetProperty(name, null, args);
        }

        /// <summary>
        /// Gets the property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>The property.</returns>
        public object GetProperty(string name, Type[] parameterTypes, object[] args)
        {
            return this.GetProperty(name, BindToEveryThing, parameterTypes, args);
        }

        /// <summary>
        /// Set the property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="value">value to set</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        public void SetProperty(string name, object value, params object[] args)
        {
            this.SetProperty(name, null, value, args);
        }

        /// <summary>
        /// Set the property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
        /// <param name="value">value to set</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        public void SetProperty(string name, Type[] parameterTypes, object value, object[] args)
        {
            this.SetProperty(name, BindToEveryThing, value, parameterTypes, args);
        }

        /// <summary>
        /// Gets the property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>The property.</returns>
        public object GetProperty(string name, BindingFlags bindingFlags, params object[] args)
        {
            return this.GetProperty(name, bindingFlags, null, args);
        }

        /// <summary>
        /// Gets the property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>The property.</returns>
        public object GetProperty(string name, BindingFlags bindingFlags, Type[] parameterTypes, object[] args)
        {
            if (parameterTypes != null)
            {
                PropertyInfo pi = this.originalType.GetProperty(name, bindingFlags, null, null, parameterTypes, null);
                if (pi == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.CurrentCulture, "The member specified ({0}) could not be found. You might need to regenerate your private accessor, or the member may be private and defined on a base class. If the latter is true, you need to pass the type that defines the member into PrivateObject's constructor.", name));
                }

                return pi.GetValue(this.target, args);
            }
            else
            {
                return this.InvokeHelper(name, bindingFlags | BindingFlags.GetProperty, args, null);
            }
        }

        /// <summary>
        /// Sets the property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="value">value to set</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        public void SetProperty(string name, BindingFlags bindingFlags, object value, params object[] args)
        {
            this.SetProperty(name, bindingFlags, value, null, args);
        }

        /// <summary>
        /// Sets the property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="bindingFlags">A bitmask comprised of one or more <see cref="T:System.Reflection.BindingFlags"/> that specify how the search is conducted.</param>
        /// <param name="value">value to set</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        public void SetProperty(string name, BindingFlags bindingFlags, object value, Type[] parameterTypes, object[] args)
        {
            if (parameterTypes != null)
            {
                PropertyInfo pi = this.originalType.GetProperty(name, bindingFlags, null, null, parameterTypes, null);
                if (pi == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.CurrentCulture, "The member specified ({0}) could not be found. You might need to regenerate your private accessor, or the member may be private and defined on a base class. If the latter is true, you need to pass the type that defines the member into PrivateObject's constructor.", name));
                }

                pi.SetValue(this.target, value, args);
            }
            else
            {
                object[] pass = new object[(args?.Length ?? 0) + 1];
                pass[0] = value;
                args?.CopyTo(pass, 1);
                this.InvokeHelper(name, bindingFlags | BindingFlags.SetProperty, pass, null);
            }
        }

        /// <summary>
        /// Validate access string
        /// </summary>
        /// <param name="access"> access string</param>
        private static void ValidateAccessString(string access)
        {
            if (access.Length == 0)
            {
                throw new ArgumentException("Access string has invalid syntax.");
            }

            string[] arr = access.Split('.');
            foreach (string str in arr)
            {
                if ((str.Length == 0) || (str.IndexOfAny(new char[] { ' ', '\t', '\n' }) != -1))
                {
                    throw new ArgumentException("Access string has invalid syntax.");
                }
            }
        }

        /// <summary>
        /// Invokes the memeber
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">Additional attributes</param>
        /// <param name="args">Arguments for the invocation</param>
        /// <param name="culture">Culture</param>
        /// <returns>Result of the invocation</returns>
        private object InvokeHelper(string name, BindingFlags bindingFlags, object[] args, CultureInfo culture)
        {
            Debug.Assert(this.target != null, "Internal Error: Null reference is returned for internal object");

            // Invoke the actual Method
            try
            {
                return this.originalType.InvokeMember(name, bindingFlags, null, this.target, args, culture);
            }
            catch (TargetInvocationException e)
            {
                Debug.Assert(e.InnerException != null, "Inner exception should not be null.");
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }

                throw;
            }
        }

        private void ConstructFrom(object obj)
        {
            this.target = obj;
            this.originalType = obj.GetType();
        }

        private void BuildGenericMethodCacheForType(Type t)
        {
            Debug.Assert(t != null, "type should not be null.");
            this.methodCache = new Dictionary<string, LinkedList<MethodInfo>>();

            MethodInfo[] members = t.GetMethods(BindToEveryThing);
            LinkedList<MethodInfo> listByName; // automatically initialized to null

            foreach (MethodInfo member in members)
            {
                if (member.IsGenericMethod || member.IsGenericMethodDefinition)
                {
                    if (!this.GenericMethodCache.TryGetValue(member.Name, out listByName))
                    {
                        listByName = new LinkedList<MethodInfo>();
                        this.GenericMethodCache.Add(member.Name, listByName);
                    }

                    Debug.Assert(listByName != null, "list should not be null.");
                    listByName.AddLast(member);
                }
            }
        }

        /// <summary>
        /// Extracts the most appropriate generic method signature from the current private type.
        /// </summary>
        /// <param name="methodName">The name of the method in which to search the signature cache.</param>
        /// <param name="parameterTypes">An array of types corresponding to the types of the parameters in which to search.</param>
        /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
        /// <param name="bindingFlags"><see cref="BindingFlags"/> to further filter the method signatures.</param>
        /// <param name="modifiers">Modifiers for parameters.</param>
        /// <returns>A methodinfo instance.</returns>
        private MethodInfo GetGenericMethodFromCache(string methodName, Type[] parameterTypes, Type[] typeArguments, BindingFlags bindingFlags, ParameterModifier[] modifiers)
        {
            Debug.Assert(!string.IsNullOrEmpty(methodName), "Invalid method name.");
            Debug.Assert(parameterTypes != null, "Invalid parameter type array.");
            Debug.Assert(typeArguments != null, "Invalid type arguments array.");

            // Build a preliminary list of method candidates that contain roughly the same signature.
            var methodCandidates = this.GetMethodCandidates(methodName, parameterTypes, typeArguments, bindingFlags, modifiers);

            // Search of ambiguous methods (methods with the same signature).
            MethodInfo[] finalCandidates = new MethodInfo[methodCandidates.Count];
            methodCandidates.CopyTo(finalCandidates, 0);

            if ((parameterTypes != null) && (parameterTypes.Length == 0))
            {
                for (int i = 0; i < finalCandidates.Length; i++)
                {
                    MethodInfo methodInfo = finalCandidates[i];

                    if (!RuntimeTypeHelper.CompareMethodSigAndName(methodInfo, finalCandidates[0]))
                    {
                        throw new AmbiguousMatchException();
                    }
                }

                // All the methods have the exact same name and sig so return the most derived one.
                return RuntimeTypeHelper.FindMostDerivedNewSlotMeth(finalCandidates, finalCandidates.Length) as MethodInfo;
            }

            // Now that we have a preliminary list of candidates, select the most appropriate one.
            return RuntimeTypeHelper.SelectMethod(bindingFlags, finalCandidates, parameterTypes, modifiers) as MethodInfo;
        }

        private LinkedList<MethodInfo> GetMethodCandidates(string methodName, Type[] parameterTypes, Type[] typeArguments, BindingFlags bindingFlags, ParameterModifier[] modifiers)
        {
            Debug.Assert(!string.IsNullOrEmpty(methodName), "methodName should not be null.");
            Debug.Assert(parameterTypes != null, "parameterTypes should not be null.");
            Debug.Assert(typeArguments != null, "typeArguments should not be null.");

            LinkedList<MethodInfo> methodCandidates = new LinkedList<MethodInfo>();
            LinkedList<MethodInfo> methods = null;

            if (!this.GenericMethodCache.TryGetValue(methodName, out methods))
            {
                return methodCandidates;
            }

            Debug.Assert(methods != null, "methods should not be null.");

            foreach (MethodInfo candidate in methods)
            {
                bool paramMatch = true;
                ParameterInfo[] candidateParams = null;
                Type[] genericArgs = candidate.GetGenericArguments();
                Type sourceParameterType = null;

                if (genericArgs.Length != typeArguments.Length)
                {
                    continue;
                }

                // Since we can't just get the correct MethodInfo from Reflection,
                // we will just match the number of parameters, their order, and their type
                var methodCandidate = candidate;
                candidateParams = methodCandidate.GetParameters();

                if (candidateParams.Length != parameterTypes.Length)
                {
                    continue;
                }

                // Exact binding
                if ((bindingFlags & BindingFlags.ExactBinding) != 0)
                {
                    int i = 0;

                    foreach (ParameterInfo candidateParam in candidateParams)
                    {
                        sourceParameterType = parameterTypes[i++];

                        if (candidateParam.ParameterType.ContainsGenericParameters)
                        {
                            // Since we have a generic parameter here, just make sure the IsArray matches.
                            if (candidateParam.ParameterType.IsArray != sourceParameterType.IsArray)
                            {
                                paramMatch = false;
                                break;
                            }
                        }
                        else
                        {
                            if (candidateParam.ParameterType != sourceParameterType)
                            {
                                paramMatch = false;
                                break;
                            }
                        }
                    }

                    if (paramMatch)
                    {
                        methodCandidates.AddLast(methodCandidate);
                        continue;
                    }
                }
                else
                {
                    methodCandidates.AddLast(methodCandidate);
                }
            }

            return methodCandidates;
        }
    }

    /// <summary>
    /// This class represents a private class for the Private Accessor functionality.
    /// </summary>
    public class PrivateType
    {
        /// <summary>
        /// Binds to everything
        /// </summary>
        private const BindingFlags BindToEveryThing = BindingFlags.Default
            | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        /// <summary>
        /// The wrapped type.
        /// </summary>
        private Type type;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateType"/> class that contains the private type.
        /// </summary>
        /// <param name="assemblyName">Assembly name</param>
        /// <param name="typeName">fully qualified name of the </param>
        public PrivateType(string assemblyName, string typeName)
        {
            Assembly asm = Assembly.Load(assemblyName);

            this.type = asm.GetType(typeName, true);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateType"/> class that contains
        /// the private type from the type object
        /// </summary>
        /// <param name="type">The wrapped Type to create.</param>
        public PrivateType(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            this.type = type;
        }

        /// <summary>
        /// Gets the referenced type
        /// </summary>
        public Type ReferencedType => this.type;

        /// <summary>
        /// Invokes static member
        /// </summary>
        /// <param name="name">Name of the member to InvokeHelper</param>
        /// <param name="args">Arguements to the invoction</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, params object[] args)
        {
            return this.InvokeStatic(name, null, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invokes static member
        /// </summary>
        /// <param name="name">Name of the member to InvokeHelper</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke</param>
        /// <param name="args">Arguements to the invoction</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, Type[] parameterTypes, object[] args)
        {
            return this.InvokeStatic(name, parameterTypes, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invokes static member
        /// </summary>
        /// <param name="name">Name of the member to InvokeHelper</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke</param>
        /// <param name="args">Arguements to the invoction</param>
        /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, Type[] parameterTypes, object[] args, Type[] typeArguments)
        {
            return this.InvokeStatic(name, BindToEveryThing, parameterTypes, args, CultureInfo.InvariantCulture, typeArguments);
        }

        /// <summary>
        /// Invokes the static method
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <param name="culture">Culture</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, object[] args, CultureInfo culture)
        {
            return this.InvokeStatic(name, null, args, culture);
        }

        /// <summary>
        /// Invokes the static method
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <param name="culture">Culture info</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, Type[] parameterTypes, object[] args, CultureInfo culture)
        {
            return this.InvokeStatic(name, BindingFlags.InvokeMethod, parameterTypes, args, culture);
        }

        /// <summary>
        /// Invokes the static method
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, BindingFlags bindingFlags, params object[] args)
        {
            return this.InvokeStatic(name, bindingFlags, null, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invokes the static method
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, BindingFlags bindingFlags, Type[] parameterTypes, object[] args)
        {
            return this.InvokeStatic(name, bindingFlags, parameterTypes, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invokes the static method
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <param name="culture">Culture</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, BindingFlags bindingFlags, object[] args, CultureInfo culture)
        {
            return this.InvokeStatic(name, bindingFlags, null, args, culture);
        }

        /// <summary>
        /// Invokes the static method
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <param name="culture">Culture</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, BindingFlags bindingFlags, Type[] parameterTypes, object[] args, CultureInfo culture)
        {
            return this.InvokeStatic(name, bindingFlags, parameterTypes, args, culture, null);
        }

        /// <summary>
        /// Invokes the static method
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the method to invoke</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <param name="culture">Culture</param>
        /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
        /// <returns>Result of invocation</returns>
        public object InvokeStatic(string name, BindingFlags bindingFlags, Type[] parameterTypes, object[] args, CultureInfo culture, Type[] typeArguments)
        {
            if (parameterTypes != null)
            {
                MethodInfo member = this.type.GetMethod(name, bindingFlags | BindToEveryThing | BindingFlags.Static, null, parameterTypes, null);
                if (member == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "The member specified ({0}) could not be found. You might need to regenerate your private accessor, or the member may be private and defined on a base class. If the latter is true, you need to pass the type that defines the member into PrivateObject's constructor.", name));
                }

                try
                {
                    if (member.IsGenericMethodDefinition)
                    {
                        MethodInfo constructed = member.MakeGenericMethod(typeArguments);
                        return constructed.Invoke(null, bindingFlags, null, args, culture);
                    }
                    else
                    {
                        return member.Invoke(null, bindingFlags, null, args, culture);
                    }
                }
                catch (TargetInvocationException e)
                {
                    Debug.Assert(e.InnerException != null, "Inner Exception should not be null.");
                    if (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }

                    throw;
                }
            }
            else
            {
                return this.InvokeHelperStatic(name, bindingFlags | BindingFlags.InvokeMethod, args, culture);
            }
        }

        /// <summary>
        /// Gets the element in static array
        /// </summary>
        /// <param name="name">Name of the array</param>
        /// <param name="indices">
        /// A one-dimensional array of 32-bit integers that represent the indexes specifying
        /// the position of the element to get. For instance, to access a[10][11] the indices would be {10,11}
        /// </param>
        /// <returns>element at the specified location</returns>
        public object GetStaticArrayElement(string name, params int[] indices)
        {
            return this.GetStaticArrayElement(name, BindToEveryThing, indices);
        }

        /// <summary>
        /// Sets the memeber of the static array
        /// </summary>
        /// <param name="name">Name of the array</param>
        /// <param name="value">value to set</param>
        /// <param name="indices">
        /// A one-dimensional array of 32-bit integers that represent the indexes specifying
        /// the position of the element to set. For instance, to access a[10][11] the array would be {10,11}
        /// </param>
        public void SetStaticArrayElement(string name, object value, params int[] indices)
        {
            this.SetStaticArrayElement(name, BindToEveryThing, value, indices);
        }

        /// <summary>
        /// Gets the element in satatic array
        /// </summary>
        /// <param name="name">Name of the array</param>
        /// <param name="bindingFlags">Additional InvokeHelper attributes</param>
        /// <param name="indices">
        /// A one-dimensional array of 32-bit integers that represent the indexes specifying
        /// the position of the element to get. For instance, to access a[10][11] the array would be {10,11}
        /// </param>
        /// <returns>element at the spcified location</returns>
        public object GetStaticArrayElement(string name, BindingFlags bindingFlags, params int[] indices)
        {
            Array arr = (Array)this.InvokeHelperStatic(name, BindingFlags.GetField | BindingFlags.GetProperty | bindingFlags, null, CultureInfo.InvariantCulture);
            return arr.GetValue(indices);
        }

        /// <summary>
        /// Sets the memeber of the static array
        /// </summary>
        /// <param name="name">Name of the array</param>
        /// <param name="bindingFlags">Additional InvokeHelper attributes</param>
        /// <param name="value">value to set</param>
        /// <param name="indices">
        /// A one-dimensional array of 32-bit integers that represent the indexes specifying
        /// the position of the element to set. For instance, to access a[10][11] the array would be {10,11}
        /// </param>
        public void SetStaticArrayElement(string name, BindingFlags bindingFlags, object value, params int[] indices)
        {
            Array arr = (Array)this.InvokeHelperStatic(name, BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Static | bindingFlags, null, CultureInfo.InvariantCulture);
            arr.SetValue(value, indices);
        }

        /// <summary>
        /// Gets the static field
        /// </summary>
        /// <param name="name">Name of the field</param>
        /// <returns>The static field.</returns>
        public object GetStaticField(string name)
        {
            return this.GetStaticField(name, BindToEveryThing);
        }

        /// <summary>
        /// Sets the static field
        /// </summary>
        /// <param name="name">Name of the field</param>
        /// <param name="value">Arguement to the invocation</param>
        public void SetStaticField(string name, object value)
        {
            this.SetStaticField(name, BindToEveryThing, value);
        }

        /// <summary>
        /// Gets the static field using specified InvokeHelper attributes
        /// </summary>
        /// <param name="name">Name of the field</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// <returns>The static field.</returns>
        public object GetStaticField(string name, BindingFlags bindingFlags)
        {
            return this.InvokeHelperStatic(name, BindingFlags.GetField | BindingFlags.Static | bindingFlags, null, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sets the static field using binding attributes
        /// </summary>
        /// <param name="name">Name of the field</param>
        /// <param name="bindingFlags">Additional InvokeHelper attributes</param>
        /// <param name="value">Arguement to the invocation</param>
        public void SetStaticField(string name, BindingFlags bindingFlags, object value)
        {
            this.InvokeHelperStatic(name, BindingFlags.SetField | bindingFlags | BindingFlags.Static, new[] { value }, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the static field or property
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <returns>The static field or property.</returns>
        public object GetStaticFieldOrProperty(string name)
        {
            return this.GetStaticFieldOrProperty(name, BindToEveryThing);
        }

        /// <summary>
        /// Sets the static field or property
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <param name="value">Value to be set to field or property</param>
        public void SetStaticFieldOrProperty(string name, object value)
        {
            this.SetStaticFieldOrProperty(name, BindToEveryThing, value);
        }

        /// <summary>
        /// Gets the static field or property using specified InvokeHelper attributes
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// <returns>The static field or property.</returns>
        public object GetStaticFieldOrProperty(string name, BindingFlags bindingFlags)
        {
            return this.InvokeHelperStatic(name, BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Static | bindingFlags, null, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sets the static field or property using binding attributes
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// <param name="value">Value to be set to field or property</param>
        public void SetStaticFieldOrProperty(string name, BindingFlags bindingFlags, object value)
        {
            this.InvokeHelperStatic(name, BindingFlags.SetField | BindingFlags.SetProperty | bindingFlags | BindingFlags.Static, new[] { value }, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the static property
        /// </summary>
        /// <param name="name">Name of the field or property</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <returns>The static property.</returns>
        public object GetStaticProperty(string name, params object[] args)
        {
            return this.GetStaticProperty(name, BindToEveryThing, args);
        }

        /// <summary>
        /// Sets the static property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="value">Value to be set to field or property</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        public void SetStaticProperty(string name, object value, params object[] args)
        {
            this.SetStaticProperty(name, BindToEveryThing, value, null, args);
        }

        /// <summary>
        /// Sets the static property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="value">Value to be set to field or property</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        public void SetStaticProperty(string name, object value, Type[] parameterTypes, object[] args)
        {
            this.SetStaticProperty(name, BindingFlags.SetProperty, value, parameterTypes, args);
        }

        /// <summary>
        /// Gets the static property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="bindingFlags">Additional invocation attributes.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>The static property.</returns>
        public object GetStaticProperty(string name, BindingFlags bindingFlags, params object[] args)
        {
            return this.GetStaticProperty(name, BindingFlags.GetProperty | BindingFlags.Static | bindingFlags, null, args);
        }

        /// <summary>
        /// Gets the static property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="bindingFlags">Additional invocation attributes.</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        /// <returns>The static property.</returns>
        public object GetStaticProperty(string name, BindingFlags bindingFlags, Type[] parameterTypes, object[] args)
        {
            if (parameterTypes != null)
            {
                PropertyInfo pi = this.type.GetProperty(name, bindingFlags | BindingFlags.Static, null, null, parameterTypes, null);
                if (pi == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "The member specified ({0}) could not be found. You might need to regenerate your private accessor, or the member may be private and defined on a base class. If the latter is true, you need to pass the type that defines the member into PrivateObject's constructor.", name));
                }

                return pi.GetValue(null, args);
            }
            else
            {
                return this.InvokeHelperStatic(name, bindingFlags | BindingFlags.GetProperty, args, null);
            }
        }

        /// <summary>
        /// Sets the static property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="bindingFlags">Additional invocation attributes.</param>
        /// <param name="value">Value to be set to field or property</param>
        /// <param name="args">Optional index values for indexed properties. The indexes of indexed properties are zero-based. This value should be null for non-indexed properties. </param>
        public void SetStaticProperty(string name, BindingFlags bindingFlags, object value, params object[] args)
        {
            this.SetStaticProperty(name, bindingFlags, value, null, args);
        }

        /// <summary>
        /// Sets the static property
        /// </summary>
        /// <param name="name">Name of the property</param>
        /// <param name="bindingFlags">Additional invocation attributes.</param>
        /// <param name="value">Value to be set to field or property</param>
        /// <param name="parameterTypes">An array of <see cref="T:System.Type"/> objects representing the number, order, and type of the parameters for the indexed property.</param>
        /// <param name="args">Arguments to pass to the member to invoke.</param>
        public void SetStaticProperty(string name, BindingFlags bindingFlags, object value, Type[] parameterTypes, object[] args)
        {
            if (parameterTypes != null)
            {
                PropertyInfo pi = this.type.GetProperty(name, bindingFlags | BindingFlags.Static, null, null, parameterTypes, null);
                if (pi == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.CurrentCulture, "The member specified ({0}) could not be found. You might need to regenerate your private accessor, or the member may be private and defined on a base class. If the latter is true, you need to pass the type that defines the member into PrivateObject's constructor.", name));
                }

                pi.SetValue(null, value, args);
            }
            else
            {
                object[] pass = new object[(args?.Length ?? 0) + 1];
                pass[0] = value;
                args?.CopyTo(pass, 1);
                this.InvokeHelperStatic(name, bindingFlags | BindingFlags.SetProperty, pass, null);
            }
        }

        /// <summary>
        /// Invokes the static method
        /// </summary>
        /// <param name="name">Name of the member</param>
        /// <param name="bindingFlags">Additional invocation attributes</param>
        /// <param name="args">Arguements to the invocation</param>
        /// <param name="culture">Culture</param>
        /// <returns>Result of invocation</returns>
        private object InvokeHelperStatic(string name, BindingFlags bindingFlags, object[] args, CultureInfo culture)
        {
            try
            {
                return this.type.InvokeMember(name, bindingFlags | BindToEveryThing | BindingFlags.Static, null, null, args, culture);
            }
            catch (TargetInvocationException e)
            {
                Debug.Assert(e.InnerException != null, "Inner Exception should not be null.");
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Provides method signature discovery for generic methods.
    /// </summary>
    internal class RuntimeTypeHelper
    {
        /// <summary>
        /// Compares the method signatures of these two methods.
        /// </summary>
        /// <param name="m1">Method1</param>
        /// <param name="m2">Method2</param>
        /// <returns>True if they are similiar.</returns>
        internal static bool CompareMethodSigAndName(MethodBase m1, MethodBase m2)
        {
            ParameterInfo[] params1 = m1.GetParameters();
            ParameterInfo[] params2 = m2.GetParameters();

            if (params1.Length != params2.Length)
            {
                return false;
            }

            int numParams = params1.Length;
            for (int i = 0; i < numParams; i++)
            {
                if (params1[i].ParameterType != params2[i].ParameterType)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the hierarchy depth from the base type of the provided type.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>The depth.</returns>
        internal static int GetHierarchyDepth(Type t)
        {
            int depth = 0;

            Type currentType = t;
            do
            {
                depth++;
                currentType = currentType.BaseType;
            }
            while (currentType != null);

            return depth;
        }

        /// <summary>
        /// Finds most dervied type with the provided information.
        /// </summary>
        /// <param name="match">Candidate matches.</param>
        /// <param name="cMatches">Number of matches.</param>
        /// <returns>The most derived method.</returns>
        internal static MethodBase FindMostDerivedNewSlotMeth(MethodBase[] match, int cMatches)
        {
            int deepestHierarchy = 0;
            MethodBase methWithDeepestHierarchy = null;

            for (int i = 0; i < cMatches; i++)
            {
                // Calculate the depth of the hierarchy of the declaring type of the
                // current method.
                int currentHierarchyDepth = GetHierarchyDepth(match[i].DeclaringType);

                // Two methods with the same hierarchy depth are not allowed. This would
                // mean that there are 2 methods with the same name and sig on a given type
                // which is not allowed, unless one of them is vararg...
                if (currentHierarchyDepth == deepestHierarchy)
                {
                    if (methWithDeepestHierarchy != null)
                    {
                        Debug.Assert(
                            methWithDeepestHierarchy != null && ((match[i].CallingConvention & CallingConventions.VarArgs)
                                                                 | (methWithDeepestHierarchy.CallingConvention & CallingConventions.VarArgs)) != 0,
                            "Calling conventions: " + match[i].CallingConvention + " - " + methWithDeepestHierarchy.CallingConvention);
                    }

                    throw new AmbiguousMatchException();
                }

                // Check to see if this method is on the most derived class.
                if (currentHierarchyDepth > deepestHierarchy)
                {
                    deepestHierarchy = currentHierarchyDepth;
                    methWithDeepestHierarchy = match[i];
                }
            }

            return methWithDeepestHierarchy;
        }

        /// <summary>
        /// Given a set of methods that match the base criteria, select a method based
        /// upon an array of types. This method should return null if no method matches
        /// the criteria.
        /// </summary>
        /// <param name="bindingAttr">Binding specification.</param>
        /// <param name="match">Candidate matches</param>
        /// <param name="types">Types</param>
        /// <param name="modifiers">Parameter modifiers.</param>
        /// <returns>Matching method. Null if none matches.</returns>
        internal static MethodBase SelectMethod(BindingFlags bindingAttr, MethodBase[] match, Type[] types, ParameterModifier[] modifiers)
        {
            if (match == null)
            {
                throw new ArgumentNullException("match");
            }

            int i;
            int j;

            Type[] realTypes = new Type[types.Length];
            for (i = 0; i < types.Length; i++)
            {
                realTypes[i] = types[i].UnderlyingSystemType;
            }

            types = realTypes;

            // If there are no methods to match to, then return null, indicating that no method
            // matches the criteria
            if (match.Length == 0)
            {
                return null;
            }

            // Find all the methods that can be described by the types parameter.
            // Remove all of them that cannot.
            int curIdx = 0;
            for (i = 0; i < match.Length; i++)
            {
                ParameterInfo[] par = match[i].GetParameters();
                if (par.Length != types.Length)
                {
                    continue;
                }

                for (j = 0; j < types.Length; j++)
                {
                    Type pCls = par[j].ParameterType;

                    if (pCls.ContainsGenericParameters)
                    {
                        if (pCls.IsArray != types[j].IsArray)
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (pCls == types[j])
                        {
                            continue;
                        }

                        if (pCls == typeof(object))
                        {
                            continue;
                        }
                        else
                        {
                            if (!pCls.IsAssignableFrom(types[j]))
                            {
                                break;
                            }
                        }
                    }
                }

                if (j == types.Length)
                {
                    match[curIdx++] = match[i];
                }
            }

            if (curIdx == 0)
            {
                return null;
            }

            if (curIdx == 1)
            {
                return match[0];
            }

            // Walk all of the methods looking the most specific method to invoke
            int currentMin = 0;
            bool ambig = false;
            int[] paramOrder = new int[types.Length];
            for (i = 0; i < types.Length; i++)
            {
                paramOrder[i] = i;
            }

            for (i = 1; i < curIdx; i++)
            {
                int newMin = FindMostSpecificMethod(match[currentMin], paramOrder, null, match[i], paramOrder, null, types, null);
                if (newMin == 0)
                {
                    ambig = true;
                }
                else
                {
                    if (newMin == 2)
                    {
                        currentMin = i;
                        ambig = false;
                        currentMin = i;
                    }
                }
            }

            if (ambig)
            {
                throw new AmbiguousMatchException();
            }

            return match[currentMin];
        }

        /// <summary>
        /// Finds the most specific method in the two methods provided.
        /// </summary>
        /// <param name="m1">Method 1</param>
        /// <param name="paramOrder1">Parameter order for Method 1</param>
        /// <param name="paramArrayType1">Paramter array type.</param>
        /// <param name="m2">Method 2</param>
        /// <param name="paramOrder2">Parameter order for Method 2</param>
        /// <param name="paramArrayType2">>Paramter array type.</param>
        /// <param name="types">Types to search in.</param>
        /// <param name="args">Args.</param>
        /// <returns>An int representing the match.</returns>
        internal static int FindMostSpecificMethod(
            MethodBase m1,
            int[] paramOrder1,
            Type paramArrayType1,
            MethodBase m2,
            int[] paramOrder2,
            Type paramArrayType2,
            Type[] types,
            object[] args)
        {
            // Find the most specific method based on the parameters.
            int res = FindMostSpecific(
                m1.GetParameters(),
                paramOrder1,
                paramArrayType1,
                m2.GetParameters(),
                paramOrder2,
                paramArrayType2,
                types,
                args);

            // If the match was not ambiguous then return the result.
            if (res != 0)
            {
                return res;
            }

            // Check to see if the methods have the exact same name and signature.
            if (CompareMethodSigAndName(m1, m2))
            {
                // Determine the depth of the declaring types for both methods.
                int hierarchyDepth1 = GetHierarchyDepth(m1.DeclaringType);
                int hierarchyDepth2 = GetHierarchyDepth(m2.DeclaringType);

                // The most derived method is the most specific one.
                if (hierarchyDepth1 == hierarchyDepth2)
                {
                    return 0;
                }
                else if (hierarchyDepth1 < hierarchyDepth2)
                {
                    return 2;
                }
                else
                {
                    return 1;
                }
            }

            // The match is ambiguous.
            return 0;
        }

        /// <summary>
        /// Finds the most specific method in the two methods provided.
        /// </summary>
        /// <param name="p1">Method 1</param>
        /// <param name="paramOrder1">Parameter order for Method 1</param>
        /// <param name="paramArrayType1">Paramter array type.</param>
        /// <param name="p2">Method 2</param>
        /// <param name="paramOrder2">Parameter order for Method 2</param>
        /// <param name="paramArrayType2">>Paramter array type.</param>
        /// <param name="types">Types to search in.</param>
        /// <param name="args">Args.</param>
        /// <returns>An int representing the match.</returns>
        internal static int FindMostSpecific(
            ParameterInfo[] p1,
            int[] paramOrder1,
            Type paramArrayType1,
            ParameterInfo[] p2,
            int[] paramOrder2,
            Type paramArrayType2,
            Type[] types,
            object[] args)
        {
            // A method using params is always less specific than one not using params
            if (paramArrayType1 != null && paramArrayType2 == null)
            {
                return 2;
            }

            if (paramArrayType2 != null && paramArrayType1 == null)
            {
                return 1;
            }

            bool p1Less = false;
            bool p2Less = false;

            for (int i = 0; i < types.Length; i++)
            {
                if (args != null && args[i] == Type.Missing)
                {
                    continue;
                }

                Type c1, c2;

                // If a param array is present, then either
                //      the user re-ordered the parameters in which case
                //          the argument to the param array is either an array
                //              in which case the params is conceptually ignored and so paramArrayType1 == null
                //          or the argument to the param array is a single element
                //              in which case paramOrder[i] == p1.Length - 1 for that element
                //      or the user did not re-order the parameters in which case
                //          the paramOrder array could contain indexes larger than p.Length - 1
                ////          so any index >= p.Length - 1 is being put in the param array

                if (paramArrayType1 != null && paramOrder1[i] >= p1.Length - 1)
                {
                    c1 = paramArrayType1;
                }
                else
                {
                    c1 = p1[paramOrder1[i]].ParameterType;
                }

                if (paramArrayType2 != null && paramOrder2[i] >= p2.Length - 1)
                {
                    c2 = paramArrayType2;
                }
                else
                {
                    c2 = p2[paramOrder2[i]].ParameterType;
                }

                if (c1 == c2)
                {
                    continue;
                }

                if (c1.ContainsGenericParameters || c2.ContainsGenericParameters)
                {
                    continue;
                }

                switch (FindMostSpecificType(c1, c2, types[i]))
                {
                    case 0:
                        return 0;
                    case 1:
                        p1Less = true;
                        break;
                    case 2:
                        p2Less = true;
                        break;
                }
            }

            // Two way p1Less and p2Less can be equal.  All the arguments are the
            //  same they both equal false, otherwise there were things that both
            //  were the most specific type on....
            if (p1Less == p2Less)
            {
                // it's possible that the 2 methods have same sig and  default param in which case we match the one
                // with the same number of args but only if they were exactly the same (that is p1Less and p2Lees are both false)
                if (!p1Less && p1.Length != p2.Length && args != null)
                {
                    if (p1.Length == args.Length)
                    {
                        return 1;
                    }
                    else if (p2.Length == args.Length)
                    {
                        return 2;
                    }
                }

                return 0;
            }
            else
            {
                return (p1Less == true) ? 1 : 2;
            }
        }

        /// <summary>
        /// Finds the most specific type in the two provided.
        /// </summary>
        /// <param name="c1">Type 1</param>
        /// <param name="c2">Type 2</param>
        /// <param name="t">The defining type</param>
        /// <returns>An int representing the match.</returns>
        internal static int FindMostSpecificType(Type c1, Type c2, Type t)
        {
            // If the two types are exact move on...
            if (c1 == c2)
            {
                return 0;
            }

            if (c1 == t)
            {
                return 1;
            }

            if (c2 == t)
            {
                return 2;
            }

            bool c1FromC2;
            bool c2FromC1;

            if (c1.IsByRef || c2.IsByRef)
            {
                if (c1.IsByRef && c2.IsByRef)
                {
                    c1 = c1.GetElementType();
                    c2 = c2.GetElementType();
                }
                else if (c1.IsByRef)
                {
                    if (c1.GetElementType() == c2)
                    {
                        return 2;
                    }

                    c1 = c1.GetElementType();
                }
                else
                {
                    if (c2.GetElementType() == c1)
                    {
                        return 1;
                    }

                    c2 = c2.GetElementType();
                }
            }

            if (c1.IsPrimitive && c2.IsPrimitive)
            {
                c1FromC2 = true;
                c2FromC1 = true;
            }
            else
            {
                c1FromC2 = c1.IsAssignableFrom(c2);
                c2FromC1 = c2.IsAssignableFrom(c1);
            }

            if (c1FromC2 == c2FromC1)
            {
                return 0;
            }

            if (c1FromC2)
            {
                return 2;
            }
            else
            {
                return 1;
            }
        }
    }
}