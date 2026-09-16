#import <Foundation/Foundation.h>
#import <sys/socket.h>
#import <sys/un.h>
#import <unistd.h>
#import <pthread.h>
#import <arpa/inet.h>
#import <sys/stat.h>
#import <objc/runtime.h>

static NSString *const kSocketPath = @"/tmp/gamepigeonfucker-injector.sock";

static pthread_mutex_t gSubscriberLock = PTHREAD_MUTEX_INITIALIZER;
static NSMutableArray<NSNumber *> *gSubscriberFds;

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

    Class messageClass = NSClassFromString(@"IMMessage");
    unsigned int count4 = 0;
    Method *methods4 = class_copyMethodList(messageClass, &count4);
    NSMutableArray *messageNames = [NSMutableArray array];
    NSMutableArray *messageInitMethods = [NSMutableArray array];
    for (unsigned int i = 0; i < count4; i++) {
        NSString *name = NSStringFromSelector(method_getName(methods4[i]));
        if ([name hasPrefix:@"init"]) {
            const char *typeEncoding = method_getTypeEncoding(methods4[i]);
            [messageInitMethods addObject:@{
                @"selector": name,
                @"typeEncoding": typeEncoding ? [NSString stringWithUTF8String:typeEncoding] : @"?",
                @"argumentCount": @(method_getNumberOfArguments(methods4[i])),
            }];
            continue;
        }
        if ([name hasPrefix:@"_"] || [name hasPrefix:@"set"]) {
            continue;
        }
        [messageNames addObject:name];
    }
    free(methods4);

    return @{ @"ok": @YES, @"IMChatRegistry_chatForHandle": names, @"IMAccount_handle_methods": acctNames, @"IMChat_lastAddressed_methods": chatNames, @"IMMessage_methods": messageNames, @"IMMessage_init_methods": messageInitMethods };
}

static NSDictionary *HandleListClasses(NSDictionary *request)
{
    NSString *filter = request[@"filter"];
    NSString *lowerFilter = [filter isKindOfClass:[NSString class]] ? filter.lowercaseString : nil;

    unsigned int count = 0;
    Class *classes = objc_copyClassList(&count);
    NSMutableArray *names = [NSMutableArray array];
    for (unsigned int i = 0; i < count; i++)
    {
        NSString *name = NSStringFromClass(classes[i]);
        if (lowerFilter.length == 0 || [name.lowercaseString containsString:lowerFilter])
        {
            [names addObject:name];
        }
    }
    free(classes);

    return @{ @"ok": @YES, @"classes": names };
}

static NSArray<NSString *> *MethodNamesForClass(Class cls)
{
    NSMutableArray *names = [NSMutableArray array];
    unsigned int count = 0;
    Method *methods = class_copyMethodList(cls, &count);
    for (unsigned int i = 0; i < count; i++)
    {
        [names addObject:NSStringFromSelector(method_getName(methods[i]))];
    }
    free(methods);
    return names;
}

static NSDictionary *HandleDumpClass(NSDictionary *request)
{
    NSString *className = request[@"className"];
    if (![className isKindOfClass:[NSString class]] || className.length == 0)
    {
        return @{ @"ok": @NO, @"error": @"className required" };
    }

    Class cls = NSClassFromString(className);
    if (!cls)
    {
        return @{ @"ok": @NO, @"error": @"class not found" };
    }

    NSMutableArray *propertyNames = [NSMutableArray array];
    unsigned int propertyCount = 0;
    objc_property_t *properties = class_copyPropertyList(cls, &propertyCount);
    for (unsigned int i = 0; i < propertyCount; i++)
    {
        [propertyNames addObject:[NSString stringWithUTF8String:property_getName(properties[i])]];
    }
    free(properties);

    NSMutableArray *ivarNames = [NSMutableArray array];
    unsigned int ivarCount = 0;
    Ivar *ivars = class_copyIvarList(cls, &ivarCount);
    for (unsigned int i = 0; i < ivarCount; i++)
    {
        [ivarNames addObject:[NSString stringWithUTF8String:ivar_getName(ivars[i])]];
    }
    free(ivars);

    NSMutableArray *superclassChain = [NSMutableArray array];
    Class super = class_getSuperclass(cls);
    while (super)
    {
        [superclassChain addObject:NSStringFromClass(super)];
        super = class_getSuperclass(super);
    }

    return @{
        @"ok": @YES,
        @"className": className,
        @"superclassChain": superclassChain,
        @"instanceMethods": MethodNamesForClass(cls),
        @"classMethods": MethodNamesForClass(object_getClass(cls)),
        @"properties": propertyNames,
        @"ivars": ivarNames,
    };
}

static NSDictionary *HandleSendViaAccount(NSDictionary *request)
{
    NSString *accountUniqueID = request[@"accountUniqueID"];
    NSString *recipientHandleID = request[@"recipientHandleID"];
    NSString *text = request[@"text"];
    NSString *balloonBundleId = request[@"balloonBundleId"];
    NSString *payloadBase64 = request[@"payloadDataBase64"];
    NSString *associatedMessageGuid = request[@"associatedMessageGuid"];

    NSData *payloadData = [payloadBase64 isKindOfClass:[NSString class]] && payloadBase64.length > 0
        ? [[NSData alloc] initWithBase64EncodedString:payloadBase64 options:0]
        : nil;

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

        Class registrarClass = NSClassFromString(@"IMChatRegistry");
        id registrar = [registrarClass performSelector:@selector(sharedInstance)];

        NSString *senderIdentityID = nil;

        id existingChat = [registrar performSelector:@selector(chatForIMHandle:) withObject:handle];
        if (existingChat && [existingChat respondsToSelector:@selector(lastAddressedHandleID)]) {
            id addressed = [existingChat performSelector:@selector(lastAddressedHandleID)];
            if ([addressed isKindOfClass:[NSString class]] && [addressed length] > 0) {
                senderIdentityID = addressed;
            }
        }

        if (!senderIdentityID) {
            NSString *requested = request[@"senderIdentityID"];
            if ([requested isKindOfClass:[NSString class]] && requested.length > 0) {
                senderIdentityID = requested;
            }
        }

        id myHandle = senderIdentityID
            ? [targetAccount performSelector:@selector(imHandleWithID:) withObject:senderIdentityID]
            : nil;

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

        if (senderIdentityID) {
            [chat performSelector:@selector(setLastAddressedHandleID:) withObject:senderIdentityID];
        }

        NSAttributedString *attributedText = [[NSAttributedString alloc] initWithString:text ?: @""];

        IMMessage *message = [[IMMessage alloc] initWithSender:myHandle
                                                            time:nil
                                                            text:attributedText
                                                  messageSubject:nil
                                               fileTransferGUIDs:nil
                                                           flags:messageFlags
                                                           error:nil
                                                            guid:nil
                                                         subject:nil
                                                 balloonBundleID:[balloonBundleId isKindOfClass:[NSString class]] && balloonBundleId.length > 0 ? balloonBundleId : nil
                                                     payloadData:payloadData
                                          expressiveSendStyleID:nil
                                                threadIdentifier:nil];
        if (!message) {
            response = @{ @"ok": @NO, @"error": @"failed to build message" };
            return;
        }

        id associatedError = [NSNull null];
        if ([associatedMessageGuid isKindOfClass:[NSString class]] && associatedMessageGuid.length > 0) {
            @try {
                // Balloon update: real GamePigeon moves are associated messages (type 2)
                // pointing at the game invite's message guid, so iOS updates the existing
                // balloon in place instead of dropping a standalone duplicate session.
                [message setValue:associatedMessageGuid forKey:@"associatedMessageGUID"];
                [message setValue:@(2) forKey:@"associatedMessageType"];
            } @catch (NSException *e) {
                associatedError = e.reason ?: @"unknown";
            }
        }

        [chat sendMessage:message];
        id sentGuid = nil;
        @try { sentGuid = [message valueForKey:@"guid"]; } @catch (NSException *e) {}
        response = @{
            @"ok": @YES,
            @"chatDescription": [chat description] ?: @"?",
            @"senderIdentityID": senderIdentityID ?: [NSNull null],
            @"messageGuid": sentGuid ?: [NSNull null],
            @"associatedMessageError": associatedError,
        };
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
    if ([request[@"cmd"] isEqual:@"listClasses"])
    {
        return HandleListClasses(request);
    }
    if ([request[@"cmd"] isEqual:@"dumpClass"])
    {
        return HandleDumpClass(request);
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
    NSString *associatedMessageGuid = request[@"associatedMessageGuid"];

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
                                                           flags:0x100005
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

        id associatedError = [NSNull null];
        if ([associatedMessageGuid isKindOfClass:[NSString class]] && associatedMessageGuid.length > 0) {
            @try {
                // Balloon update: see HandleSendViaAccount.
                [message setValue:associatedMessageGuid forKey:@"associatedMessageGUID"];
                [message setValue:@(2) forKey:@"associatedMessageType"];
            } @catch (NSException *e) {
                associatedError = e.reason ?: @"unknown";
            }
        }

        [chat sendMessage:message];
        id sentGuid = nil;
        @try { sentGuid = [message valueForKey:@"guid"]; } @catch (NSException *e) {}
        response = @{
            @"ok": @YES,
            @"messageGuid": sentGuid ?: [NSNull null],
            @"associatedMessageError": associatedError,
        };
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

static void *ConnectionThread(void *arg)
{
    int clientFd = (int)(intptr_t)arg;

    @autoreleasepool
    {
        NSData *requestData = ReadFrame(clientFd);
        NSDictionary *request = requestData
            ? [NSJSONSerialization JSONObjectWithData:requestData options:0 error:nil]
            : nil;

        if ([request isKindOfClass:[NSDictionary class]] && [request[@"cmd"] isEqual:@"subscribe"])
        {
            NSData *ack = [NSJSONSerialization dataWithJSONObject:@{ @"ok": @YES } options:0 error:nil];
            WriteFrame(clientFd, ack);

            pthread_mutex_lock(&gSubscriberLock);
            [gSubscriberFds addObject:@(clientFd)];
            pthread_mutex_unlock(&gSubscriberLock);

            uint8_t discard[64];
            while (read(clientFd, discard, sizeof(discard)) > 0) { }

            pthread_mutex_lock(&gSubscriberLock);
            [gSubscriberFds removeObject:@(clientFd)];
            pthread_mutex_unlock(&gSubscriberLock);
        }
        else
        {
            NSDictionary *response = [request isKindOfClass:[NSDictionary class]]
                ? HandleRequest(request)
                : @{ @"ok": @NO, @"error": @"bad request" };
            NSData *responseData = [NSJSONSerialization dataWithJSONObject:response options:0 error:nil];
            WriteFrame(clientFd, responseData);
        }
    }

    close(clientFd);
    return NULL;
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

    if (listen(serverFd, 8) != 0)
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

        pthread_t thread;
        pthread_create(&thread, NULL, ConnectionThread, (void *)(intptr_t)clientFd);
        pthread_detach(thread);
    }

    return NULL;
}

static double SecondsSinceMacEpoch(id dateValue)
{
    return [dateValue isKindOfClass:[NSDate class]] ? [(NSDate *)dateValue timeIntervalSinceReferenceDate] : 0.0;
}

static id ValueOrNull(id value)
{
    return value ?: [NSNull null];
}

static NSDictionary *BuildMessageDict(id message)
{
    if (!message)
    {
        return nil;
    }

    NSMutableDictionary *d = [NSMutableDictionary dictionary];

    @try { d[@"guid"] = ValueOrNull([message valueForKey:@"guid"]); } @catch (NSException *e) {}
    @try { d[@"text"] = ValueOrNull([message valueForKey:@"plainBody"]); } @catch (NSException *e) {}
    @try {
        id sender = [message valueForKey:@"sender"];
        d[@"senderHandleId"] = ValueOrNull([sender valueForKey:@"id"]);
    } @catch (NSException *e) {}
    @try { d[@"isFromMe"] = @([[message valueForKey:@"isFromMe"] boolValue]); } @catch (NSException *e) { d[@"isFromMe"] = @NO; }
    @try { d[@"isEmpty"] = @([[message valueForKey:@"isEmpty"] boolValue]); } @catch (NSException *e) { d[@"isEmpty"] = @NO; }
    @try { d[@"isSent"] = @([[message valueForKey:@"isSent"] boolValue]); } @catch (NSException *e) { d[@"isSent"] = @NO; }
    @try { d[@"isDelivered"] = @([[message valueForKey:@"isDelivered"] boolValue]); } @catch (NSException *e) { d[@"isDelivered"] = @NO; }
    @try { d[@"isRead"] = @([[message valueForKey:@"isRead"] boolValue]); } @catch (NSException *e) { d[@"isRead"] = @NO; }
    @try { d[@"isFinished"] = @([[message valueForKey:@"isFinished"] boolValue]); } @catch (NSException *e) { d[@"isFinished"] = @NO; }
    @try { d[@"timeSeconds"] = @(SecondsSinceMacEpoch([message valueForKey:@"time"])); } @catch (NSException *e) { d[@"timeSeconds"] = @0; }
    @try { d[@"timeDeliveredSeconds"] = @(SecondsSinceMacEpoch([message valueForKey:@"timeDelivered"])); } @catch (NSException *e) { d[@"timeDeliveredSeconds"] = @0; }
    @try { d[@"timeReadSeconds"] = @(SecondsSinceMacEpoch([message valueForKey:@"timeRead"])); } @catch (NSException *e) { d[@"timeReadSeconds"] = @0; }
    @try { d[@"balloonBundleId"] = ValueOrNull([message valueForKey:@"balloonBundleID"]); } @catch (NSException *e) {}
    @try {
        NSData *payload = [message valueForKey:@"payloadData"];
        d[@"payloadDataBase64"] = payload.length > 0 ? [payload base64EncodedStringWithOptions:0] : [NSNull null];
    } @catch (NSException *e) {}
    @try { d[@"isAssociatedMessage"] = @([[message valueForKey:@"isAssociatedMessage"] boolValue]); } @catch (NSException *e) { d[@"isAssociatedMessage"] = @NO; }
    @try { d[@"associatedMessageGuid"] = ValueOrNull([message valueForKey:@"associatedMessageGUID"]); } @catch (NSException *e) {}
    @try { d[@"associatedMessageType"] = ValueOrNull([message valueForKey:@"associatedMessageType"]); } @catch (NSException *e) {}
    @try { d[@"associatedMessageEmoji"] = ValueOrNull([message valueForKey:@"associatedMessageEmoji"]); } @catch (NSException *e) {}
    @try { d[@"isReply"] = @([[message valueForKey:@"isReply"] boolValue]); } @catch (NSException *e) { d[@"isReply"] = @NO; }
    @try { d[@"threadIdentifier"] = ValueOrNull([message valueForKey:@"threadIdentifier"]); } @catch (NSException *e) {}
    @try { d[@"hasEditedParts"] = @([[message valueForKey:@"hasEditedParts"] boolValue]); } @catch (NSException *e) { d[@"hasEditedParts"] = @NO; }
    @try { d[@"dateEditedSeconds"] = @(SecondsSinceMacEpoch([message valueForKey:@"dateEdited"])); } @catch (NSException *e) { d[@"dateEditedSeconds"] = @0; }
    @try { d[@"hasRetractedParts"] = @([[message valueForKey:@"hasRetractedParts"] boolValue]); } @catch (NSException *e) { d[@"hasRetractedParts"] = @NO; }
    @try { d[@"flags"] = ValueOrNull([message valueForKey:@"flags"]); } @catch (NSException *e) {}

    return d;
}

static void BroadcastToSubscribers(NSDictionary *eventDict)
{
    NSData *payload = [NSJSONSerialization dataWithJSONObject:eventDict options:0 error:nil];
    if (!payload)
    {
        return;
    }

    NSMutableArray<NSNumber *> *deadFds = [NSMutableArray array];

    pthread_mutex_lock(&gSubscriberLock);
    NSArray<NSNumber *> *fds = [gSubscriberFds copy];
    pthread_mutex_unlock(&gSubscriberLock);

    for (NSNumber *fdNum in fds)
    {
        int fd = fdNum.intValue;
        uint32_t length = htonl((uint32_t)payload.length);
        if (write(fd, &length, sizeof(length)) != (ssize_t)sizeof(length) ||
            write(fd, payload.bytes, payload.length) != (ssize_t)payload.length)
        {
            [deadFds addObject:fdNum];
        }
    }

    if (deadFds.count > 0)
    {
        pthread_mutex_lock(&gSubscriberLock);
        [gSubscriberFds removeObjectsInArray:deadFds];
        pthread_mutex_unlock(&gSubscriberLock);
    }
}

static void InstallMessageWatcher(void)
{
    [[NSNotificationCenter defaultCenter] addObserverForName:@"__kIMChatMessageDidChangeNotification"
                                                        object:nil
                                                         queue:nil
                                                    usingBlock:^(NSNotification *note) {
        id chat = note.object;
        NSDictionary *newMessage = BuildMessageDict(note.userInfo[@"__kIMChatValueKey"]);
        if (!newMessage)
        {
            return;
        }
        NSDictionary *oldMessage = BuildMessageDict(note.userInfo[@"__kIMChatOldValueKey"]);

        NSMutableDictionary *event = [NSMutableDictionary dictionary];
        @try { event[@"chatIdentifier"] = ValueOrNull([chat valueForKey:@"chatIdentifier"]); } @catch (NSException *e) {}
        @try { event[@"chatGuid"] = ValueOrNull([chat valueForKey:@"guid"]); } @catch (NSException *e) {}
        event[@"new"] = newMessage;
        event[@"old"] = oldMessage ?: [NSNull null];

        BroadcastToSubscribers(event);
    }];
}

__attribute__((constructor))
static void MessagesInjectorInit(void)
{
    gSubscriberFds = [NSMutableArray array];
    InstallMessageWatcher();

    pthread_t thread;
    pthread_create(&thread, NULL, AcceptLoop, NULL);
}
