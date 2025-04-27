#pragma once
#import <Foundation/Foundation.h>

// C functions exposed to Unity

#ifdef __cplusplus
extern "C" {
#endif

bool _balancyWebViewOpen(const char* url, int width, int height, bool transparent);
void _balancyWebViewClose();
bool _balancyWebViewSendMessage(const char* message);
void _balancyWebViewSetOfflineCacheEnabled(bool enabled);

#ifdef __cplusplus
}
#endif