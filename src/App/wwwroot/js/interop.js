window.loadFont = (url) => {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = url;
    document.head.appendChild(link);
};

window.saveAsFile = (fileName, contentType, base64String) => {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64String}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Функция для создания и активации скачивания
window.createDownloadLink = (fileId, fileName, base64String, contentType) => {
    // Создаем элемент div для хранения ссылки
    const container = document.getElementById('download-container') || (() => {
        const div = document.createElement('div');
        div.id = 'download-container';
        div.style.display = 'none';
        document.body.appendChild(div);
        return div;
    })();

    // Создаем blob и ссылку
    const byteCharacters = atob(base64String);
    const byteArrays = [];

    for (let offset = 0; offset < byteCharacters.length; offset += 512) {
        const slice = byteCharacters.slice(offset, offset + 512);

        const byteNumbers = new Array(slice.length);
        for (let i = 0; i < slice.length; i++) {
            byteNumbers[i] = slice.charCodeAt(i);
        }

        const byteArray = new Uint8Array(byteNumbers);
        byteArrays.push(byteArray);
    }

    const blob = new Blob(byteArrays, {type: contentType});
    const url = URL.createObjectURL(blob);

    // Создаем ссылку
    const link = document.createElement('a');
    link.href = url;
    link.id = 'download-link-' + fileId;
    link.download = fileName;
    link.innerHTML = 'Скачать ' + fileName;
    link.className = 'download-link mud-button-root mud-button mud-button-filled mud-button-filled-primary mud-button-filled-size-medium mud-ripple';
    link.style.margin = '10px';
    link.style.display = 'inline-block';
    link.style.padding = '8px 16px';

    // Добавляем ссылку в контейнер
    container.style.display = 'block';
    container.style.position = 'fixed';
    container.style.bottom = '20px';
    container.style.right = '20px';
    container.style.zIndex = '1000';
    container.style.background = '#fff';
    container.style.padding = '10px';
    container.style.borderRadius = '4px';
    container.style.boxShadow = '0 2px 10px rgba(0,0,0,0.2)';

    container.appendChild(link);

    // Устанавливаем автоматическое удаление через 5 минут
    setTimeout(() => {
        const linkElement = document.getElementById('download-link-' + fileId);
        if (linkElement) {
            linkElement.remove();
            URL.revokeObjectURL(url);

            // Если больше нет ссылок, скрываем контейнер
            if (container.children.length === 0) {
                container.style.display = 'none';
            }
        }
    }, 300000); // 5 минут

    return url;
};

// Функция для инициирования скачивания
window.triggerFileDownload = (fileName, url) => {
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};