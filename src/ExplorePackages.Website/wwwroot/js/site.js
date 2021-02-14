$(function () {
    $('input[name="useCustomMax"]').on('click', function () {
        $(this).parent().siblings('.custom-max').toggle();
    });
})
