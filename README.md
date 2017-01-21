# Reactive Essentials - Event Sourcing

## 설치

관계형 데이터베이스를 이벤트 저장소로 사용하는 경우는 다음 패키지를 설치합니다.

```
> Install-Package ReactiveArchitecture.EventSourcing.Sql
```

Azure 테이블 저장소를 이벤트 저장소로 사용하는 경우는 다음 패키지를 설치합니다.

```
> Install-Package ReactiveArchitecture.EventSourcing.Azure
```

Azure Event Hubs를 이벤트 발행을 위한 메시지 버스로 사용하는 경우는 다음 패키지를 설치합니다.

```
> Install-Package ReactiveArchitecture.Messaging.Azure
```

## 예제 응용프로그램

- [TodoList](examples/TodoList)
