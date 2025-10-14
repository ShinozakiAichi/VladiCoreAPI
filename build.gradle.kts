plugins {
    java
    id("com.github.johnrengelman.shadow") version "8.1.1"
    id("io.papermc.paperweight.userdev") version "1.7.1"
    id("com.diffplug.spotless") version "6.25.0"
    id("org.jetbrains.kotlinx.kover") version "0.8.3"
}

group = "net.cosmomine"
version = "0.1.0"
description = "CosmoMINE Screens Core"

repositories {
    mavenCentral()
    // Paper dev-bundle тут живет
    maven("https://repo.papermc.io/repository/maven-public/")
    // Остальные тебе по сути не нужны, но оставлю, мало ли
    maven("https://repo.papermc.io/repository/maven-releases/")
    maven("https://repo.spongepowered.org/repository/maven-public/")
}

dependencies {
    // ВАЖНО: это и есть фикс — подключаем dev-бандл для paperweight
    paperweightDevBundle("io.papermc.paper:dev-bundle:1.21.1-R0.1-SNAPSHOT")

    // Плагины-соседи — только compileOnly
    compileOnly("me.clip:placeholderapi:2.11.6")
    compileOnly("io.github.miniplaceholders:miniplaceholders-api:2.2.3")
    compileOnly("net.luckperms:api:5.4")

    // Шейдим конфигурат
    implementation("org.spongepowered:configurate-yaml:4.2.0-SNAPSHOT")

    testImplementation("org.junit.jupiter:junit-jupiter:5.10.2")
    testImplementation("org.mockito:mockito-core:5.12.0")
}

java {
    toolchain.languageVersion.set(JavaLanguageVersion.of(21))
}

tasks {
    compileJava {
        options.encoding = "UTF-8"
        // release 21, без цирка
        options.release.set(21)
    }

    test { useJUnitPlatform() }

    jar {
        // Открой глаза: обычный jar нам не нужен, используем shadow + reobf
        enabled = false
    }

    shadowJar {
        archiveBaseName.set("CosmoMINE-Screens")
        archiveClassifier.set("")
        archiveVersion.set(version.toString())

        // Чтоб не ловить конфликты, шейдим Configurate внутрь
        relocate("org.spongepowered.configurate", "net.cosmomine.libs.configurate")
        minimize()
    }

    // reobf после shadow — иначе поймаешь «веселье» на сервере
    // paperweight 1.7.1: используем inputJar
    reobfJar {
        inputJar.set(shadowJar.get().archiveFile)
    }

    assemble {
        dependsOn(reobfJar)
    }
}

spotless {
    java {
        googleJavaFormat()
        indentWithSpaces()
    }
}
