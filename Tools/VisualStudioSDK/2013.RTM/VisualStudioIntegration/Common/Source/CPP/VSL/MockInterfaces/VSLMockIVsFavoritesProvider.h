/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

This code is a part of the Visual Studio Library.

***************************************************************************/

#ifndef IVSFAVORITESPROVIDER_H_10C49CA1_2F46_11D3_A504_00C04F5E0BA5
#define IVSFAVORITESPROVIDER_H_10C49CA1_2F46_11D3_A504_00C04F5E0BA5

#if _MSC_VER > 1000
#pragma once
#endif

#include "vsbrowse.h"

#pragma warning(push)
#pragma warning(disable : 4510) // default constructor could not be generated
#pragma warning(disable : 4610) // can never be instantiated - user defined constructor required
#pragma warning(disable : 4512) // assignment operator could not be generated
#pragma warning(disable : 6011) // Dereferencing NULL pointer (a NULL derference is just another kind of failure for a unit test

namespace VSL
{

class IVsFavoritesProviderNotImpl :
	public IVsFavoritesProvider
{

VSL_DECLARE_NONINSTANTIABLE_BASE_CLASS(IVsFavoritesProviderNotImpl)

public:

	typedef IVsFavoritesProvider Interface;

	STDMETHOD(Navigate)(
		/*[in]*/ LPCOLESTR /*lpszURL*/,
		/*[in]*/ DWORD /*dwFlags*/,
		/*[out]*/ IVsWindowFrame** /*ppFrame*/)VSL_STDMETHOD_NOTIMPL
};

class IVsFavoritesProviderMockImpl :
	public IVsFavoritesProvider,
	public MockBase
{

VSL_DECLARE_NONINSTANTIABLE_BASE_CLASS(IVsFavoritesProviderMockImpl)

public:

VSL_DEFINE_MOCK_CLASS_TYPDEFS(IVsFavoritesProviderMockImpl)

	typedef IVsFavoritesProvider Interface;
	struct NavigateValidValues
	{
		/*[in]*/ LPCOLESTR lpszURL;
		/*[in]*/ DWORD dwFlags;
		/*[out]*/ IVsWindowFrame** ppFrame;
		HRESULT retValue;
	};

	STDMETHOD(Navigate)(
		/*[in]*/ LPCOLESTR lpszURL,
		/*[in]*/ DWORD dwFlags,
		/*[out]*/ IVsWindowFrame** ppFrame)
	{
		VSL_DEFINE_MOCK_METHOD(Navigate)

		VSL_CHECK_VALIDVALUE_STRINGW(lpszURL);

		VSL_CHECK_VALIDVALUE(dwFlags);

		VSL_SET_VALIDVALUE_INTERFACE(ppFrame);

		VSL_RETURN_VALIDVALUES();
	}
};


} // namespace VSL

#pragma warning(pop)

#endif // IVSFAVORITESPROVIDER_H_10C49CA1_2F46_11D3_A504_00C04F5E0BA5
