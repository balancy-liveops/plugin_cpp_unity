#!/usr/bin/env python3
"""Execute the production iOS Foundation URL/response mapper on macOS.
This does not substitute for the WKWebView simulator/device integration test.
"""
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
source = (root / 'WebView/Plugins/iOS/BalancyWebView.mm').read_text()
helpers = source[source.index('static NSString* const kBalancyLocalScheme'):source.index('static NSString* BalancyMimeTypeForPath')]
cases = r'''
static int checks;
static void Check(BOOL pass, NSString* name) {
    if (!pass) { NSLog(@"FAIL %@", name); exit(1); }
    ++checks;
}
static NSString* Encode(id value) {
    return [[NSString alloc] initWithData:[NSJSONSerialization dataWithJSONObject:value options:0 error:nil] encoding:NSUTF8StringEncoding];
}
static id Decode(NSString* value) {
    return [NSJSONSerialization JSONObjectWithData:[value dataUsingEncoding:NSUTF8StringEncoding] options:0 error:nil];
}
int main() { @autoreleasepool {
    NSString* base = [NSTemporaryDirectory() stringByAppendingPathComponent:NSUUID.UUID.UUIDString];
    gBalancyPersistentDataRootPath = [base stringByAppendingPathComponent:@"Documents"];
    gBalancyStreamingAssetsRootPath = [base stringByAppendingPathComponent:@"App/Data/Raw"];
    NSString* image = [NSURL fileURLWithPath:[gBalancyPersistentDataRootPath stringByAppendingPathComponent:@"Balancy/Models/game_Cache/Files/icon.png"]].absoluteString;
    NSString* font = [NSURL fileURLWithPath:[gBalancyStreamingAssetsRootPath stringByAppendingPathComponent:@"Balancy/font ü # %.ttf"]].absoluteString;
    NSDictionary* single = Decode(BalancyMessageWithLocalResourceURLs(Encode(@{@"type":@"response",@"id":@"11",@"result":image,@"extra":@42})));
    Check([single[@"result"] isEqual:@"balancy-local://local/persistent/Balancy/Models/game_Cache/Files/icon.png"], @"downloaded image");
    Check([single[@"id"] isEqual:@"11"] && [single[@"extra"] isEqual:@42], @"preserve envelope fields");
    NSArray* inputs = @[@{@"id":@"1",@"result":image}, @{@"id":@"2",@"result":font}, @{@"id":@"3",@"error":@"failed"}, @{@"id":@"4",@"result":@"https://cdn.test/icon.png"}, @{@"id":@"5",@"result":@{@"path":image}}, @{@"id":@"6",@"result":@"data:image/png;base64,AAAA"}, NSNull.null];
    NSArray* output = Decode(BalancyMessageWithLocalResourceURLs(Encode(@{@"type":@"batch-response",@"responses":inputs})))[@"responses"];
    Check([output[0][@"result"] isEqual:single[@"result"]], @"batch image without per-item type");
    NSURL* fontURL = [NSURL URLWithString:output[1][@"result"]];
    Check([fontURL.scheme isEqual:@"balancy-local"] && [fontURL.path.precomposedStringWithCanonicalMapping isEqual:@"/streaming/Balancy/font ü # %.ttf"], @"packaged font and URL escaping");
    for (int i=2;i<7;i++) Check([output[i] isEqual:inputs[i]], @"unrelated batch result preserved");
    for (id value in @[@{@"type":@"custom",@"result":image}, @{@"type":@"loadView",@"owner":@{@"result":image}}, @[@"file:///other"]]) {
        NSString* text=Encode(value); Check([BalancyMessageWithLocalResourceURLs(text) isEqual:text], @"non-response unchanged");
    }
    Check([BalancyMessageWithLocalResourceURLs(@"not json file://") isEqual:@"not json file://"], @"malformed message unchanged");
    NSString* outside=Encode(@{@"type":@"response",@"result":@"file:///etc/passwd"});
    Check([BalancyMessageWithLocalResourceURLs(outside) isEqual:outside], @"outside configured roots unchanged");
    NSString* already=Encode(@{@"type":@"response",@"result":single[@"result"]});
    Check([BalancyMessageWithLocalResourceURLs(already) isEqual:already], @"idempotent");
    gBalancyPersistentDataRootPath=nil;
    NSString* unconfigured=Encode(@{@"type":@"response",@"result":image});
    Check([BalancyMessageWithLocalResourceURLs(unconfigured) isEqual:unconfigured], @"unconfigured root unchanged");
    printf("PASS %d iOS resource URL checks\n", checks);
} }
'''
with tempfile.TemporaryDirectory(prefix='balancy-ios-resource-test-') as tmp:
    src = Path(tmp) / 'test.mm'
    binary = Path(tmp) / 'test'
    src.write_text('#import <Foundation/Foundation.h>\n' + helpers + cases)
    env = os.environ.copy()
    env.setdefault('DEVELOPER_DIR', '/Library/Developer/CommandLineTools')
    subprocess.run(['xcrun', 'clang++', '-std=c++17', '-fobjc-arc', '-Wno-nullability-completeness', '-Wno-incompatible-pointer-types', '-framework', 'Foundation', str(src), '-o', str(binary)], check=True, env=env)
    subprocess.run([str(binary)], check=True)
