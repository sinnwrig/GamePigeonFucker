#import <Foundation/Foundation.h>
#import <sys/socket.h>
#import <sys/un.h>
#import <unistd.h>
#import <pthread.h>
#import <arpa/inet.h>
#import <sys/stat.h>
#import <objc/runtime.h>

static NSString *const kSocketPath = @"/tmp/gamepigeonfucker-injector.sock";

@interface IMMessage : NSObject
- (instancetype)initWithSender:(id)sender
                           time:(id)time
                           text:(NSAttributedString *)text
                 messageSubject:(id)subject
              fileTransferGUIDs:(id)guids
                          flags:(unsigned long long)flags
                          error:(id)error
                           guid:(NSString *)guid
                        subject:(id)subject2
                balloonBundleID:(NSString *)balloonBundleID
                    payloadData:(NSData *)payloadData
          expressiveSendStyleID:(id)styleID
                threadIdentifier:(id)threadIdentifier;
@end

@interface IMChat : NSObject
- (void)sendMessage:(IMMessage *)message;
@end

@interface IMChatRegistry : NSObject
+ (instancetype)sharedInstance;
- (IMChat *)existingChatWithGUID:(NSString *)guid;
@end

static NSDictionary *HandleListAccounts(void)
{
    __block NSDictionary *response = nil;
    dispatch_sync(dispatch_get_main_queue(), ^{
        Class accountControllerClass = NSClassFromString(@"IMAccountController");
        id controller = [accountControllerClass sharedInstance];
        NSArray *accounts = [controller valueForKey:@"accounts"];
        NSMutableArray *accountDumps = [NSMutableArray array];
        for (id acct in accounts) {
            NSMutableDictionary *d = [NSMutableDictionary dictionary];
            d[@"class"] = NSStringFromClass([acct class]);
            @try { d[@"uniqueID"] = [[acct valueForKey:@"uniqueID"] description] ?: @"nil"; } @catch (NSException *e) { d[@"uniqueID_error"] = e.reason ?: @"?"; }
            @try { d[@"loginIMHandle"] = [[acct valueForKey:@"loginIMHandle"] description] ?: @"nil"; } @catch (NSException *e) { d[@"loginIMHandle_error"] = e.reason ?: @"?"; }
            @try {
                id handles = [acct valueForKey:@"aliases"];
                d[@"aliases"] = [handles description] ?: @"nil";
            } @catch (NSException *e) { d[@"aliases_error"] = e.reason ?: @"?"; }
            @try {
                id loginHandles = [acct valueForKey:@"loginHandles"];
                d[@"loginHandles"] = [loginHandles description] ?: @"nil";
            } @catch (NSException *e) { d[@"loginHandles_error"] = e.reason ?: @"?"; }
            [accountDumps addObject:d];
        }
        response = @{ @"ok": @YES, @"accounts": accountDumps };
    });
    return response;
}

static NSDictionary *HandleIntrospect(void)
{
    Class registrarClass = NSClassFromString(@"IMChatRegistry");
    unsigned int count = 0;
    Method *methods = class_copyMethodList(registrarClass, &count);
    NSMutableArray *names = [NSMutableArray array];
    for (unsigned int i = 0; i < count; i++) {
        NSString *name = NSStringFromSelector(method_getName(methods[i]));
        if ([name.lowercaseString containsString:@"chat"] || [name.lowercaseString containsString:@"account"]) {
            [names addObject:name];
        }
    }
    free(methods);

    Class accountClass = NSClassFromString(@"IMAccount");
    unsigned int count2 = 0;
    Method *methods2 = class_copyMethodList(accountClass, &count2);
    NSMutableArray *acctNames = [NSMutableArray array];
    for (unsigned int i = 0; i < count2; i++) {
        NSString *name = NSStringFromSelector(method_getName(methods2[i]));
        if ([name.lowercaseString containsString:@"handle"]) {
            [acctNames addObject:name];
        }
    }
    free(methods2);

    Class chatClass = NSClassFromString(@"IMChat");
    unsigned int count3 = 0;
    Method *methods3 = class_copyMethodList(chatClass, &count3);
    NSMutableArray *chatNames = [NSMutableArray array];
    for (unsigned int i = 0; i < count3; i++) {
        NSString *name = NSStringFromSelector(method_getName(methods3[i]));
        if ([name.lowercaseString containsString:@"lastaddressed"] || [name.lowercaseString containsString:@"setaccount"]) {
            [chatNames addObject:name];
        }
    }
    free(methods3);

    return @{ @"ok": @YES, @"IMChatRegistry_chatForHandle": names, @"IMAccount_handle_methods": acctNames, @"IMChat_lastAddressed_methods": chatNames };
}

static NSDictionary *HandleSendViaAccount(NSDictionary *request)
{
    NSString *accountUniqueID = request[@"accountUniqueID"];
    NSString *recipientHandleID = request[@"recipientHandleID"];
    NSString *text = request[@"text"];

    __block NSDictionary *response = nil;
    dispatch_sync(dispatch_get_main_queue(), ^{
        Class accountControllerClass = NSClassFromString(@"IMAccountController");
        id controller = [accountControllerClass sharedInstance];
        NSArray *accounts = [controller valueForKey:@"accounts"];

        id targetAccount = nil;
        for (id acct in accounts) {
            if ([[[acct valueForKey:@"uniqueID"] description] isEqualToString:accountUniqueID]) {
                targetAccount = acct;
                break;
            }
        }
        if (!targetAccount) {
            response = @{ @"ok": @NO, @"error": @"account not found" };
            return;
        }

        id handle = [targetAccount performSelector:@selector(imHandleWithID:) withObject:recipientHandleID];
        if (!handle) {
            response = @{ @"ok": @NO, @"error": @"could not resolve handle" };
            return;
        }

        id myHandle = nil;
        NSString *senderIdentityID = request[@"senderIdentityID"];
        if ([senderIdentityID isKindOfClass:[NSString class]] && senderIdentityID.length > 0) {
            myHandle = [targetAccount performSelector:@selector(imHandleWithID:) withObject:senderIdentityID];
        } else {
            myHandle = nil;
        }

        Class registrarClass = NSClassFromString(@"IMChatRegistry");
        id registrar = [registrarClass performSelector:@selector(sharedInstance)];

        id chat = nil;
        if (myHandle) {
            SEL sel = NSSelectorFromString(@"chatForIMHandle:lastAddressedHandle:lastAddressedSIMID:");
            NSMethodSignature *sig = [registrar methodSignatureForSelector:sel];
            NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
            inv.selector = sel;
            inv.target = registrar;
            [inv setArgument:&handle atIndex:2];
            [inv setArgument:&myHandle atIndex:3];
            id nilSimId = nil;
            [inv setArgument:&nilSimId atIndex:4];
            [inv invoke];
            void *rawResult = NULL;
            [inv getReturnValue:&rawResult];
            chat = (__bridge id)rawResult;
        } else {
            chat = [registrar performSelector:@selector(chatForIMHandle:) withObject:handle];
        }
        if (!chat) {
            response = @{ @"ok": @NO, @"error": @"could not get/create chat" };
            return;
        }

        if ([senderIdentityID isKindOfClass:[NSString class]] && senderIdentityID.length > 0) {
            [chat performSelector:@selector(setLastAddressedHandleID:) withObject:senderIdentityID];
        }

        NSAttributedString *attributedText = [[NSAttributedString alloc] initWithString:text ?: @""];

        IMMessage *message = [[IMMessage alloc] initWithSender:nil
                                                            time:nil
                                                            text:attributedText
                                                  messageSubject:nil
                                               fileTransferGUIDs:nil
                                                           flags:100005
                                                           error:nil
                                                            guid:nil
                                                         subject:nil
                                                 balloonBundleID:nil
                                                     payloadData:nil
                                          expressiveSendStyleID:nil
                                                threadIdentifier:nil];
        if (!message) {
            response = @{ @"ok": @NO, @"error": @"failed to build message" };
            return;
        }

        [chat sendMessage:message];
        response = @{ @"ok": @YES, @"chatDescription": [chat description] ?: @"?" };
    });
    return response;
}

static NSDictionary *HandleRequest(NSDictionary *request)
{
    if ([request[@"cmd"] isEqual:@"listAccounts"])
    {
        return HandleListAccounts();
    }
    if ([request[@"cmd"] isEqual:@"introspect"])
    {
        return HandleIntrospect();
    }
    if ([request[@"cmd"] isEqual:@"sendViaAccount"])
    {
        return HandleSendViaAccount(request);
    }

    NSString *chatGuid = request[@"chatGuid"];
    NSString *text = request[@"text"];
    NSString *balloonBundleId = request[@"balloonBundleId"];
    NSString *payloadBase64 = request[@"payloadDataBase64"];
    NSString *senderHandle = request[@"senderHandle"];

    NSData *payloadData = [payloadBase64 isKindOfClass:[NSString class]] && payloadBase64.length > 0
        ? [[NSData alloc] initWithBase64EncodedString:payloadBase64 options:0]
        : nil;

    __block NSDictionary *response = nil;

    dispatch_sync(dispatch_get_main_queue(), ^{
        IMChat *chat = [[IMChatRegistry sharedInstance] existingChatWithGUID:chatGuid];
        if (!chat)
        {
            response = @{ @"ok": @NO, @"error": @"chat not found" };
            return;
        }

        NSAttributedString *attributedText = [[NSAttributedString alloc] initWithString:text ?: @""];

        IMMessage *message = [[IMMessage alloc] initWithSender:([senderHandle isKindOfClass:[NSString class]] && senderHandle.length > 0 ? senderHandle : nil)
                                                            time:nil
                                                            text:attributedText
                                                  messageSubject:nil
                                               fileTransferGUIDs:nil
                                                           flags:100005
                                                           error:nil
                                                            guid:nil
                                                         subject:nil
                                                 balloonBundleID:[balloonBundleId isKindOfClass:[NSString class]] && balloonBundleId.length > 0 ? balloonBundleId : nil
                                                     payloadData:payloadData
                                          expressiveSendStyleID:nil
                                                threadIdentifier:nil];
        if (!message)
        {
            response = @{ @"ok": @NO, @"error": @"failed to build message" };
            return;
        }

        [chat sendMessage:message];
        response = @{ @"ok": @YES };
    });

    return response;
}

static void WriteAll(int fd, const void *buf, size_t len)
{
    size_t sent = 0;
    while (sent < len)
    {
        ssize_t n = write(fd, (const char *)buf + sent, len - sent);
        if (n <= 0)
        {
            return;
        }
        sent += (size_t)n;
    }
}

static NSData *ReadFrame(int fd)
{
    uint32_t length = 0;
    ssize_t n = read(fd, &length, sizeof(length));
    if (n != sizeof(length))
    {
        return nil;
    }

    length = ntohl(length);
    NSMutableData *data = [NSMutableData dataWithLength:length];
    size_t received = 0;
    while (received < length)
    {
        ssize_t r = read(fd, (uint8_t *)data.mutableBytes + received, length - received);
        if (r <= 0)
        {
            return nil;
        }
        received += (size_t)r;
    }

    return data;
}

static void WriteFrame(int fd, NSData *payload)
{
    uint32_t length = htonl((uint32_t)payload.length);
    WriteAll(fd, &length, sizeof(length));
    WriteAll(fd, payload.bytes, payload.length);
}

static void *AcceptLoop(void *arg)
{
    unlink(kSocketPath.UTF8String);

    int serverFd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (serverFd < 0)
    {
        return NULL;
    }

    struct sockaddr_un addr;
    memset(&addr, 0, sizeof(addr));
    addr.sun_family = AF_UNIX;
    strncpy(addr.sun_path, kSocketPath.UTF8String, sizeof(addr.sun_path) - 1);

    if (bind(serverFd, (struct sockaddr *)&addr, sizeof(addr)) != 0)
    {
        return NULL;
    }

    chmod(kSocketPath.UTF8String, 0600);

    if (listen(serverFd, 4) != 0)
    {
        return NULL;
    }

    while (1)
    {
        int clientFd = accept(serverFd, NULL, NULL);
        if (clientFd < 0)
        {
            continue;
        }

        @autoreleasepool
        {
            NSData *requestData = ReadFrame(clientFd);
            if (requestData)
            {
                NSDictionary *request = [NSJSONSerialization JSONObjectWithData:requestData options:0 error:nil];
                NSDictionary *response = [request isKindOfClass:[NSDictionary class]]
                    ? HandleRequest(request)
                    : @{ @"ok": @NO, @"error": @"bad request" };
                NSData *responseData = [NSJSONSerialization dataWithJSONObject:response options:0 error:nil];
                WriteFrame(clientFd, responseData);
            }
        }

        close(clientFd);
    }

    return NULL;
}

__attribute__((constructor))
static void MessagesInjectorInit(void)
{
    pthread_t thread;
    pthread_create(&thread, NULL, AcceptLoop, NULL);
}
